FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ApiForge.HeadlessCms.sln
RUN dotnet publish src/ApiForge.Api/ApiForge.Api.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=7080
COPY --from=build /app/publish .
RUN mkdir -p /app/uploads
EXPOSE 7080
ENTRYPOINT ["dotnet", "ApiForge.Api.dll"]
