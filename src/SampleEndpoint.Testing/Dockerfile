# Build context: vNext/
# docker build -f SampleEndpoint.Testing/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for layer-cached restore
COPY NServiceBus.IntegrationTesting.Agent.v10/NServiceBus.IntegrationTesting.Agent.v10.csproj NServiceBus.IntegrationTesting.Agent.v10/
COPY SampleMessages/SampleMessages.csproj SampleMessages/
COPY SampleEndpoint/SampleEndpoint.csproj SampleEndpoint/
COPY SampleEndpoint.Testing/SampleEndpoint.Testing.csproj SampleEndpoint.Testing/
COPY proto/ proto/

RUN dotnet restore SampleEndpoint.Testing/SampleEndpoint.Testing.csproj

# Copy source and publish
COPY NServiceBus.IntegrationTesting.Agent.v10/ NServiceBus.IntegrationTesting.Agent.v10/
COPY SampleMessages/ SampleMessages/
COPY SampleEndpoint/ SampleEndpoint/
COPY SampleEndpoint.Testing/ SampleEndpoint.Testing/

RUN dotnet publish SampleEndpoint.Testing/SampleEndpoint.Testing.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SampleEndpoint.Testing.dll"]
