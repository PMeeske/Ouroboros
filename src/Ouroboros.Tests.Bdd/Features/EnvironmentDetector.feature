Feature: Environment Detector
    As a developer
    I want to detect the runtime environment
    So that I can configure the application appropriately for local vs cloud deployment

    Background:
        Given a fresh environment detector context

    Scenario: Development environment is detected as local
        Given ASPNETCORE_ENVIRONMENT is set to "Development"
        When I check if running in local development
        Then it should return true

    Scenario: Local environment is detected as local
        Given ASPNETCORE_ENVIRONMENT is set to "Local"
        When I check if running in local development
        Then it should return true

    Scenario: Production environment is not local
        Given ASPNETCORE_ENVIRONMENT is set to "Production"
        When I check if running in local development
        Then it should return false

    Scenario: Staging environment is not local
        Given ASPNETCORE_ENVIRONMENT is set to "Staging"
        When I check if running in local development
        Then it should return false

    Scenario: Localhost Ollama endpoint indicates local development
        Given OLLAMA_ENDPOINT is set to "http://localhost:11434"
        When I check if running in local development
        Then it should return true

    Scenario: 127.0.0.1 Ollama endpoint indicates local development
        Given OLLAMA_ENDPOINT is set to "http://127.0.0.1:11434"
        When I check if running in local development
        Then it should return true

    Scenario: Remote Ollama endpoint indicates cloud deployment
        Given OLLAMA_ENDPOINT is set to "https://ollama.example.com"
        When I check if running in local development
        Then it should return false

    Scenario: Kubernetes environment indicates cloud deployment
        Given KUBERNETES_SERVICE_HOST environment variable exists
        When I check if running in local development
        Then it should return false

    Scenario: Docker Desktop Kubernetes is detected as local
        Given KUBERNETES_SERVICE_HOST contains "docker-desktop"
        When I check if running in local development
        Then it should return true

    Scenario: Null environment variables default to local
        Given all environment variables are unset
        When I check if running in local development
        Then it should return true
