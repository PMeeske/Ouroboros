Feature: Prompts
    As a developer
    I want to use prompt templates for reasoning stages
    So that I can generate consistent prompts for Draft, Critique, and Improve stages

    Background:
        Given a fresh prompts context

    Scenario: Draft prompt is not null
        When I access the Draft prompt
        Then the prompt should not be null

    Scenario: Critique prompt is not null
        When I access the Critique prompt
        Then the prompt should not be null

    Scenario: Improve prompt is not null
        When I access the Improve prompt
        Then the prompt should not be null

    Scenario: Draft prompt formats with placeholders
        Given a Draft prompt with placeholders
        When I format with tools schemas, context, and topic
        Then the formatted prompt should contain the tools schemas
        And the formatted prompt should contain the context
        And the formatted prompt should contain the topic

    Scenario: Critique prompt formats with draft placeholder
        Given a Critique prompt with draft placeholder
        When I format with a draft response
        Then the formatted prompt should contain the draft response

    Scenario: Improve prompt formats with critique placeholder
        Given an Improve prompt with critique placeholder
        When I format with a critique response
        Then the formatted prompt should contain the critique response

    Scenario: All prompts are distinct objects
        When I access Draft, Critique, and Improve prompts
        Then Draft should not equal Critique
        And Draft should not equal Improve
        And Critique should not equal Improve
