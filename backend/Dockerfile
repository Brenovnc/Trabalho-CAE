FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY StudyPlatform.Api/StudyPlatform.Api.csproj StudyPlatform.Api/
RUN dotnet restore StudyPlatform.Api/StudyPlatform.Api.csproj
COPY StudyPlatform.Api/ StudyPlatform.Api/
RUN dotnet publish StudyPlatform.Api/StudyPlatform.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=\"http://+:${PORT:-80}\"; exec dotnet StudyPlatform.Api.dll"]
