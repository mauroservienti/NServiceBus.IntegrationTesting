# Build context: vNext/
# docker build -f SampleEndpoint/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer-cached restore
COPY NServiceBus.IntegrationTesting.Agent/NServiceBus.IntegrationTesting.Agent.csproj NServiceBus.IntegrationTesting.Agent/
COPY SampleMessages/SampleMessages.csproj SampleMessages/
COPY SampleEndpoint/SampleEndpoint.csproj SampleEndpoint/
COPY proto/ proto/

RUN dotnet restore SampleEndpoint/SampleEndpoint.csproj

# Copy source and publish
COPY NServiceBus.IntegrationTesting.Agent/ NServiceBus.IntegrationTesting.Agent/
COPY SampleMessages/ SampleMessages/
COPY SampleEndpoint/ SampleEndpoint/

RUN dotnet publish SampleEndpoint/SampleEndpoint.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SampleEndpoint.dll"]
