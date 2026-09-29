FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DoffinCaseman.sln ./
COPY src/DoffinCaseman.Web/DoffinCaseman.Web.csproj src/DoffinCaseman.Web/
COPY tests/DoffinCaseman.Web.Tests/DoffinCaseman.Web.Tests.csproj tests/DoffinCaseman.Web.Tests/
RUN dotnet restore src/DoffinCaseman.Web/DoffinCaseman.Web.csproj
COPY . .
RUN dotnet publish src/DoffinCaseman.Web/DoffinCaseman.Web.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "DoffinCaseman.Web.dll"]
