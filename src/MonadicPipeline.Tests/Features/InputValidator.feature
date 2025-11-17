Feature: Input Validator
    As a developer
    I want to validate and sanitize user input
    So that I can protect against security vulnerabilities

    Background:
        Given a fresh input validator context

    Scenario: Valid input returns success
        Given an input validator
        When I validate "This is a valid input"
        Then the validation should succeed
        And the sanitized value should be "This is a valid input"
        And there should be no errors

    Scenario: Empty input when not allowed returns failure
        Given an input validator with empty not allowed
        When I validate empty string
        Then the validation should fail
        And there should be an error about empty input

    Scenario: Empty input when allowed returns success
        Given an input validator with empty allowed
        When I validate empty string
        Then the validation should succeed

    Scenario: Input too long returns failure
        Given an input validator with max length 100
        When I validate a string of 101 characters
        Then the validation should fail
        And there should be an error about maximum length

    Scenario: Input too short returns failure
        Given an input validator with min length 5
        When I validate "ab"
        Then the validation should fail
        And there should be an error about minimum length

    Scenario Outline: SQL injection patterns are blocked
        Given an input validator
        When I validate "<malicious_input>"
        Then the validation should fail
        And there should be an error about SQL injection

        Examples:
            | malicious_input                 |
            | '; DROP TABLE users--           |
            | 1' OR '1'='1                    |
            | UNION SELECT * FROM passwords   |

    Scenario Outline: Script injection patterns are blocked
        Given an input validator
        When I validate "<malicious_input>"
        Then the validation should fail
        And there should be an error about script injection

        Examples:
            | malicious_input                      |
            | <script>alert('xss')</script>        |
            | javascript:alert('xss')              |
            | <iframe src='evil.com'></iframe>     |

    Scenario Outline: Command injection patterns are blocked
        Given an input validator
        When I validate "<malicious_input>"
        Then the validation should fail
        And there should be an error about command injection

        Examples:
            | malicious_input    |
            | cat /etc/passwd    |
            | cmd.exe && dir     |
            | $(whoami)          |

    Scenario Outline: Control characters are blocked
        Given an input validator
        When I validate "<input>"
        Then the validation should fail
        And there should be an error about null bytes
        
        Examples:
            | input         |
            | Hello\0World  |

    Scenario: Blocked characters are rejected
        Given an input validator with blocked characters
        When I validate "Hello<World>"
        Then the validation should fail
        And there should be an error about blocked character

    Scenario Outline: Whitespace trimming when enabled
        Given an input validator with trimming enabled
        When I validate "<input>"
        Then the validation should succeed
        And the sanitized value should be "<expected>"
        
        Examples:
            | input            | expected    |
            |   Hello World    | Hello World |

    Scenario Outline: Line ending normalization when enabled
        Given an input validator with line ending normalization
        When I validate "<input>"
        Then the validation should succeed
        And the sanitized value should be "<expected>"
        
        Examples:
            | input                | expected           |
            | Line1\r\nLine2\rLine3 | Line1\nLine2\nLine3 |

    Scenario Outline: HTML escaping when enabled
        Given a lenient input validator with HTML escaping
        When I validate "<input>"
        Then the validation should succeed
        And the sanitized value should contain "<expected>"
        
        Examples:
            | input                      | expected |
            | <div>Hello & goodbye</div> | &lt;     |
            | <div>Hello & goodbye</div> | &gt;     |
            | <div>Hello & goodbye</div> | &amp;    |

    Scenario: Strict validation context has strict settings
        Given a strict validation context
        Then the max length should be 1000
        And HTML escaping should be enabled
        And blocked characters should include angle brackets

    Scenario: Tool parameter context has reasonable defaults
        Given a tool parameter validation context
        Then the max length should be 5000
        And whitespace trimming should be enabled
        And line ending normalization should be enabled
