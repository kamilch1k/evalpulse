FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY EvalPulse.slnx ./
COPY src ./src
COPY tests ./tests
RUN dotnet build EvalPulse.slnx --configuration Release
RUN dotnet publish src/EvalPulse.Api/EvalPulse.Api.csproj --configuration Release --no-build --output /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "EvalPulse.Api.dll"]
