# Build context: src/
# docker build -f YetAnotherEndpoint.Testing/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for layer-cached restore
COPY NServiceBus.IntegrationTesting.Agent.v8/NServiceBus.IntegrationTesting.Agent.v8.csproj NServiceBus.IntegrationTesting.Agent.v8/
COPY NServiceBus.IntegrationTesting.Agent.v10/NServiceBus.IntegrationTesting.Agent.v10.csproj NServiceBus.IntegrationTesting.Agent.v10/
COPY SampleMessages/SampleMessages.csproj SampleMessages/
COPY YetAnotherEndpoint/YetAnotherEndpoint.csproj YetAnotherEndpoint/
COPY YetAnotherEndpoint.Testing/YetAnotherEndpoint.Testing.csproj YetAnotherEndpoint.Testing/
COPY proto/ proto/

RUN dotnet restore YetAnotherEndpoint.Testing/YetAnotherEndpoint.Testing.csproj

# Copy source and publish.
# Agent.v8 has only a .csproj; its .cs sources are linked from Agent.v10 (single source of truth).
COPY NServiceBus.IntegrationTesting.Agent.v8/ NServiceBus.IntegrationTesting.Agent.v8/
COPY NServiceBus.IntegrationTesting.Agent.v10/ NServiceBus.IntegrationTesting.Agent.v10/
COPY SampleMessages/ SampleMessages/
COPY YetAnotherEndpoint/ YetAnotherEndpoint/
COPY YetAnotherEndpoint.Testing/ YetAnotherEndpoint.Testing/

RUN dotnet publish YetAnotherEndpoint.Testing/YetAnotherEndpoint.Testing.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "YetAnotherEndpoint.Testing.dll"]
