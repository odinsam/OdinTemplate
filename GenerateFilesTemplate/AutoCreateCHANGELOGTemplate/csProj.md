    <Import Project="./common.props" />
    <PropertyGroup>
        <TargetFramework>net5.0</TargetFramework>
        <GeneratePackageOnBuild>$(OdinGeneratePackageOnBuild)</GeneratePackageOnBuild>
        <AssemblyName>$(OdinAssemblyName)</AssemblyName>
        <RootNamespace>OdinPlugs.OdinInject</RootNamespace>
        <OutputType>Library</OutputType>
        <PackageId>$(OdinPackageId)</PackageId>
        <Authors>$(OdinAuthors)</Authors>
        <Company>$(OdinCompany)</Company>
        <Copyright>$(OdinCopyright)</Copyright>
        <Product>$(OdinProduct)</Product>
        <Description>$(OdinDescription)</Description>
        <Version>1.0.5</Version>
        <PackageTags>$(OdinPackageTags)</PackageTags>
        <PackageProjectUrl>$(OdinGitUrl)/OdinPlugs.Utils.git</PackageProjectUrl>
        <RepositoryUrl>$(OdinGitUrl)/$(OdinAssemblyName)</RepositoryUrl>
        <RepositoryType>$(OdinRepositoryType)</RepositoryType>
        <RepositoryBranch>master</RepositoryBranch>
        <ApplicationIcon />
        <StartupObject />
        <PackageIcon>icon.png</PackageIcon>
    </PropertyGroup>
    <ItemGroup>
        <None Include="images\icon.png" Pack="true" PackagePath=""/>
    </ItemGroup>