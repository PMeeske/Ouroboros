Feature: Metrics Collector
    As a developer
    I want to collect application metrics
    So that I can monitor performance and usage patterns

    Background:
        Given a fresh metrics collector

    Scenario: Increment counter tracks single value
        When I increment counter "test_counter" by 5.0
        Then the counter "test_counter" should have value 5.0

    Scenario: Increment counter multiple times accumulates
        When I increment counter "test_counter" by 5.0
        And I increment counter "test_counter" by 3.0
        And I increment counter "test_counter" by 2.0
        Then the counter "test_counter" should have value 10.0

    Scenario: Increment counter with labels tracks separately
        When I increment counter "test_counter" by 5.0 with label "status=success"
        And I increment counter "test_counter" by 3.0 with label "status=failure"
        Then the counter "test_counter" with "status=success" should have value 5.0
        And the counter "test_counter" with "status=failure" should have value 3.0

    Scenario: Set gauge updates value
        When I set gauge "test_gauge" to 100.0
        And I set gauge "test_gauge" to 150.0
        Then the gauge "test_gauge" should have value 150.0

    Scenario: Observe histogram calculates statistics
        When I observe histogram "test_histogram" with value 10.0
        And I observe histogram "test_histogram" with value 20.0
        And I observe histogram "test_histogram" with value 30.0
        Then the histogram "test_histogram" should have count 3
        And the histogram "test_histogram" should have sum 60.0
        And the histogram "test_histogram" should have average 20.0

    Scenario: Histogram with labels tracks separately
        When I observe histogram "response_time" with value 100.0 with label "endpoint=/api/users"
        And I observe histogram "response_time" with value 200.0 with label "endpoint=/api/orders"
        Then the histogram "response_time" with "endpoint=/api/users" should have count 1
        And the histogram "response_time" with "endpoint=/api/orders" should have count 1

    Scenario: Get all metrics returns collection
        When I increment counter "counter1" by 5.0
        And I set gauge "gauge1" to 100.0
        And I observe histogram "hist1" with value 50.0
        Then I should have 3 metrics in the collection

    Scenario: Reset metrics clears all data
        Given I have collected some metrics
        When I reset the metrics collector
        Then I should have 0 metrics in the collection

    Scenario: Counter does not decrease
        When I increment counter "test_counter" by 10.0
        And I increment counter "test_counter" by -5.0
        Then the counter "test_counter" should have value 10.0

    Scenario: Gauge can increase and decrease
        When I set gauge "temperature" to 20.0
        And I set gauge "temperature" to 15.0
        And I set gauge "temperature" to 25.0
        Then the gauge "temperature" should have value 25.0

    Scenario: Histogram percentiles are calculated
        When I observe histogram "latency" with values 1, 2, 3, 4, 5, 6, 7, 8, 9, 10
        Then the histogram "latency" p50 should be approximately 5.0
        And the histogram "latency" p95 should be approximately 9.5
        And the histogram "latency" p99 should be approximately 10.0

    Scenario: Metrics with same name but different labels are distinct
        When I increment counter "requests" by 100 with label "status=200"
        And I increment counter "requests" by 10 with label "status=404"
        And I increment counter "requests" by 5 with label "status=500"
        Then the counter "requests" with "status=200" should have value 100
        And the counter "requests" with "status=404" should have value 10
        And the counter "requests" with "status=500" should have value 5
