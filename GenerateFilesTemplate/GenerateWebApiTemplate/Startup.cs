using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using AspectCore.Configuration;
using AspectCore.Extensions.DependencyInjection;
using GenerateWebApiTemplate.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Ocelot.DependencyInjection;
using Ocelot.Provider.Consul;
using Ocelot.Provider.Polly;
using OdinPlugs.ApiLinkMonitor.MiddlewareExtensions;
using OdinPlugs.ApiLinkMonitor.OdinAspectCore.IOdinAspectCoreInterface;
using OdinPlugs.ApiLinkMonitor.OdinMiddleware.MiddlewareExtensions;
using OdinPlugs.OdinInject.InjectCore;
using OdinPlugs.OdinInject.InjectPlugs;
using OdinPlugs.OdinInject.InjectPlugs.OdinCacheManagerInject;
using OdinPlugs.OdinUtils.OdinExtensions.BasicExtensions.OdinString;
using OdinPlugs.OdinUtils.OdinJson.ContractResolver;
using OdinPlugs.OdinUtils.OdinJson.ContractResolver.DateTimeContractResolver;
using OdinPlugs.OdinUtils.Utils.OdinFiles;
using OdinPlugs.OdinWebApi.OdinCore.ConfigModel;
using OdinPlugs.OdinWebApi.OdinCore.ConfigModel.Utils;
using OdinPlugs.OdinWebApi.OdinMAF.OdinInject;
using OdinPlugs.OdinWebApi.OdinMvcCore.OdinExtensions;
using OdinPlugs.OdinWebApi.OdinMvcCore.OdinFilter;
using Serilog;
using Serilog.Events;
using SqlSugar;
using SqlSugar.IOC;
using Microsoft.IdentityModel.Tokens;
namespace GenerateWebApiTemplate
{
    public class Startup
    {
        private IOptionsSnapshot<ProjectExtendsOptions> _iOptions;
        private ProjectExtendsOptions _Options;
        public IConfiguration Configuration { get; set; }
        public Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            EnumEnvironment enumEnvironment = configuration.GetSection("ProjectConfigOptions:EnvironmentName").Value.ToUpper().ToEnum<EnumEnvironment>();
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix: "ASPNETCORE_")
                .Add(new JsonConfigurationSource { Path = "serverConfig/cnf.json", Optional = false, ReloadOnChange = true })
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables();
            // ~ 按需加载对应项目环境的config
            // ^ 需要注意的是，如果多个配置文件有相同的配置信息，那么后加载的配置文件会覆盖先加载的配置文件(必须是.json格式的配置文件)
            // ~ 按运行环境 加载对应配置文件 
            // ~ 递归serviceConfig文件夹内所有的配置文件 加载及 cnf.config文件 以外的所有配置,
            var rootPath = webHostEnvironment.ContentRootPath + FileHelper.DirectorySeparatorChar; // 获取项目绝对路径
            ConfigLoadHelper.LoadConfigs(enumEnvironment.ToString().ToLower(), Path.Combine(Directory.GetCurrentDirectory(), "serverConfig"), config, rootPath);
            Configuration = config.Build();
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            #region Log设置
            Log.Logger = new LoggerConfiguration()
                // 最小的日志输出级别
                .MinimumLevel.Information()
                //.MinimumLevel.Information ()
                // 日志调用类命名空间如果以 System 开头，覆盖日志输出最小级别为 Information
                .MinimumLevel.Override("System", LogEventLevel.Information)
                // 日志调用类命名空间如果以 Microsoft 开头，覆盖日志输出最小级别为 Information
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .CreateLogger();
            #endregion

            Log.Information("启用【 强类型配置文件 】");
            services.Configure<ProjectExtendsOptions>(Configuration.GetSection("ProjectConfigOptions"));
            services.SetServiceProvider();
            _iOptions = services.GetService<IOptionsSnapshot<ProjectExtendsOptions>>();
            _Options = _iOptions.Value;

            Log.Logger.Information("启用【 项目基础注入 】--- 包括:当前项目注入、options、雪花Id、mongodb、redis、CacheManager、CapEventBus、Canal、Cap、链路、Nosql、RabbitMQ、mapster注入");
            Log.Logger.Information("关于mapster更多详见:https://github.com/MapsterMapper/Mapster");
            services
                .AddSingleton<ConfigOptions>(_Options)
                .AddOdinTransientInject(this.GetType().Assembly)
                .AddOdinInject(_Options)
                .AddOdinHttpClient("OdinClient")
                .AddOdinMapsterTypeAdapter(opt =>
                {
                    // mapster 映射规则 config
                    // opt.ForType<ErrorCode_DbModel, ErrorCode_Model>()
                    //         .Map(dest => dest.ShowMessage, src => src.CodeShowMessage)
                    //         .Map(dest => dest.ErrorMessage, src => src.CodeErrorMessage);
                })
                .AddOdinTransientInject(Assembly.Load("OdinPlugs.ApiLinkMonitor"))
                .AddOdinTransientInject(Assembly.Load("OdinPlugs.OdinNoSql"))
                .AddOdinTransientInject(Assembly.Load("OdinPlugs.OdinMQ"));
            services.SetServiceProvider();


            Log.Logger.Information("启用【 数据库配置 】--- 开始配置");
            Log.Logger.Information("关于SqlSugar更多详见:https://www.donet5.com/Doc/1/1180");
            SugarIocServices.AddSqlSugar(new IocConfig()
            {
                ConfigId = "1",
                ConnectionString = _Options.DbEntity.ConnectionString,
                DbType = IocDbType.MySql,
                IsAutoCloseConnection = true, //自动释放
            });
            services.ConfigurationSugar(db =>
            {
                db.CurrentConnectionConfig.ConfigureExternalServices = new ConfigureExternalServices
                {
                    DataInfoCacheService = services.GetService<IOdinCacheManager>()
                };
            });

            Log.Logger.Information("启用【 Consul 】---开始配置");
            if (_Options.Consul != null && _Options.Consul.Enable)
            {
                var ocelotBuilder = services.AddOcelot(Configuration);
                ocelotBuilder.AddConsul().AddPolly();
            }

            Log.Logger.Information("启用【 中文乱码设置 】---开始配置");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

            Log.Logger.Information("启用【 跨域配置 】---开始配置");
            string withOrigins = _Options.CrossDomain.AllowOrigin.WithOrigins;
            string policyName = _Options.CrossDomain.AllowOrigin.PolicyName;
            services.AddCors(opts =>
            {
                opts.AddPolicy(policyName, policy =>
                {
                    policy.WithOrigins(withOrigins.Split(','))
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });

            Log.Logger.Information("启用【 版本控制 】---开始配置");
            services.AddApiVersioning(option =>
            {
                //当设置为 true 时, API 将返回响应标头中支持的版本信息。
                option.ReportApiVersions = true;
                //此选项将用于不提供版本的请求。默认情况下, 假定的 API 版本为1.0。
                option.AssumeDefaultVersionWhenUnspecified = true;
                // 默认版本号
                option.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(_Options.ApiVersion.MajorVersion, _Options.ApiVersion.MinorVersion);
            }).AddResponseCompression();

            Log.Logger.Information("启用【 真实Ip获取 】---开始配置");
            services.AddHttpContextAccessor();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            Log.Logger.Information("启用【 mvc框架 】---开始配置 【  1.添加自定义过滤器\t2.controller返回json大小写控制 默认大小写 】 ");
            services.AddControllers(opt =>
                {
                    opt.Filters.Add<HttpGlobalExceptionFilter>();
                    opt.Filters.Add<OdinModelValidationFilter>(1);
                    opt.Filters.Add<ApiInvokerFilterAttribute>(2);
                    opt.Filters.Add<ApiInvokerResultFilter>();
                })
                .AddNewtonsoftJson(opt =>
                {
                    // 原样输出，后台属性怎么写的，返回的 json 就是怎样的
                    opt.SerializerSettings.ContractResolver = OdinJsonConverter.SetOdinJsonConverter(enumOdinJsonConverter.Default);
                    // 如字段为null值，该字段不会返回到前端
                    opt.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                    // 添加时间转换规则
                    opt.SerializerSettings.Converters.Add(new OdinPlugs.OdinUtils.OdinJson.ContractResolver.DateTimeContractResolver.DateTimeConverter("yyyy-MM-dd HH:mm:ss"));
                    opt.SerializerSettings.Converters.Add(new DateTimeNullableConverter("yyyy-MM-dd HH:mm:ss"));
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
                .ConfigureApiBehaviorOptions(o =>
                {
                    // 关闭框架自带的模型验证
                    o.SuppressModelStateInvalidFilter = true;
                });

            Log.Logger.Information("启用【 AspectCore 依赖注入 和 代理注册 】---开始配置");
            services.ConfigureDynamicProxy(config =>
            {
                // ~ 特性注入
                // config.Interceptors.AddServiced<FoobarAttribute>();

                // ~ 类型数注入
                // config.Interceptors.AddTyped<FoobarAttribute>();

                // ~ 带参数注入
                // config.Interceptors.AddTyped<OdinAspectCoreInterceptorAttribute>(new Object[] { "d" }, new AspectPredicate[] { });

                // ~ App1命名空间下的Service不会被代理
                // config.NonAspectPredicates.AddNamespace("App1");

                // ~ 最后一级为App1的命名空间下的Service不会被代理
                // config.NonAspectPredicates.AddNamespace("*.App1");

                // ~ ICustomService接口不会被代理
                // config.NonAspectPredicates.AddService("ICustomService");

                // ~ 后缀为Service的接口和类不会被代理
                // config.NonAspectPredicates.AddService("*Service");

                // ~ 命名为Query的方法不会被代理
                // config.NonAspectPredicates.AddMethod("Query");

                // ~ 后缀为Query的方法不会被代理
                // config.NonAspectPredicates.AddMethod("*Query");

                // ~ 带有Service后缀的类的全局拦截器
                // config.Interceptors.AddTyped<CustomInterceptorAttribute>(method => method.Name.EndsWith("MethodName"));

                // ~ 使用通配符的特定全局拦截器  注入  *Service 后缀的类
                config.Interceptors.AddTyped<OdinAspectCoreInterceptorAttribute>(Predicates.ForService("*Service"));
            });



            Log.Logger.Information("启用【 id4 】---开始配置");
            services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.Authority = _Options.IdentityServer.Authority;
                    options.RequireHttpsMetadata = _Options.IdentityServer.RequireHttpsMetadata;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = _Options.IdentityServer.ValidateAudience
                    };
                });

            services.SetServiceProvider();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment env,
            ILoggerFactory loggerFactory,
            IOptionsSnapshot<ProjectExtendsOptions> _iOptions,
            IActionDescriptorCollectionProvider actionProvider,
            IHttpContextAccessor svp)
        {
            MvcContext.httpContextAccessor = svp;
            var options = _iOptions.Value;

            app.UseStaticFiles();
            app.UseOdinApiLinkMonitor(
                // 添加需要过滤 无需链路监控的RequestPath
                opts =>
                {
                    opts.Add(@"\/knife4j");
                }
            );

            app.UseOdinException();

            app.UseSwagger();

            app.UseHttpsRedirection();

            loggerFactory.AddSerilog();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseCors(options.CrossDomain.AllowOrigin.PolicyName);

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
