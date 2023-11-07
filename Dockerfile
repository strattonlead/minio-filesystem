FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet nuget add source --username strattonlead --password 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 --store-password-in-clear-text --name createif-labs "https://nuget.pkg.github.com/strattonlead/index.json"
RUN dotnet build "Minio.FileSystem.WebApi/Minio.FileSystem.WebApi.csproj" --configuration Release -o /app/build

FROM build AS publish
RUN dotnet publish "Minio.FileSystem.WebApi/Minio.FileSystem.WebApi.csproj" --configuration Release -o /app/publish

FROM base AS final

RUN apt-get update && apt-get install -y libgdiplus
RUN apt-get -y update && apt-get -y upgrade && apt-get install -y --no-install-recommends ffmpeg

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Minio.FileSystem.WebApi.dll"]