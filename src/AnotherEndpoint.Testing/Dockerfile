# Build context: src/
# docker build -f AnotherEndpoint.Testing/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for layer-cached restore
COPY NServiceBus.IntegrationTesting.Agent.v9/NServiceBus.IntegrationTesting.Agent.v9.csproj NServiceBus.IntegrationTesting.Agent.v9/
COPY NServiceBus.IntegrationTesting.Agent.v10/NServiceBus.IntegrationTesting.Agent.v10.csproj NServiceBus.IntegrationTesting.Agent.v10/
COPY SampleMessages/SampleMessages.csproj SampleMessages/
COPY AnotherEndpoint/AnotherEndpoint.csproj AnotherEndpoint/
COPY AnotherEndpoint.Testing/AnotherEndpoint.Testing.csproj AnotherEndpoint.Testing/
COPY proto/ proto/

RUN dotnet restore AnotherEndpoint.Testing/AnotherEndpoint.Testing.csproj

# Copy source and publish
# Agent.v9 has only a .csproj; its .cs sources are linked from Agent.v10 (single source of truth).
COPY NServiceBus.IntegrationTesting.Agent.v9/ NServiceBus.IntegrationTesting.Agent.v9/
COPY NServiceBus.IntegrationTesting.Agent.v10/ NServiceBus.IntegrationTesting.Agent.v10/
COPY SampleMessages/ SampleMessages/
COPY AnotherEndpoint/ AnotherEndpoint/
COPY AnotherEndpoint.Testing/ AnotherEndpoint.Testing/

RUN dotnet publish AnotherEndpoint.Testing/AnotherEndpoint.Testing.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AnotherEndpoint.Testing.dll"]
