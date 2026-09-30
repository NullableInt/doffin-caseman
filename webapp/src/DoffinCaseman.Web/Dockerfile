FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DoffinCaseman.sln Directory.Build.props Directory.Packages.props ./
COPY src/DoffinCaseman.Web/DoffinCaseman.Web.csproj src/DoffinCaseman.Web/
COPY tests/DoffinCaseman.Web.Tests/DoffinCaseman.Web.Tests.csproj tests/DoffinCaseman.Web.Tests/
RUN dotnet restore src/DoffinCaseman.Web/DoffinCaseman.Web.csproj
COPY . .
RUN dotnet publish src/DoffinCaseman.Web/DoffinCaseman.Web.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DoffinCaseman.Web.dll"]
