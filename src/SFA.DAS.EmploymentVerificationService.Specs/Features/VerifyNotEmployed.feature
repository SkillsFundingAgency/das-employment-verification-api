Feature: Verify that a learner is not employed
    EVS registers an employment check and reports
    the result to the requesting service.

    Scenario: Receive pre-employment check request - new request
        Given a VerifyNotEmployed check has been published by a consuming service
        When EVS receives the request
        And the same request has not been received previously
        Then the request details are stored in the EVS EmploymentVerificationRequest table with Request Status 'In Progress' and NumberOfAttempts '1'
        And EVS constructs the outgoing RegisterCheckRequest to the Employment Check service
        And the request is submitted

    Scenario: Process response - NotEmployed = true
        Given the employment check result has been received from the Employment Check service
        And the employment check request has completed successfully (no errors or exceptions)
        And the result returns an employed = 'false' result (success response)
        When the result is processed
        And the result is matched to the verify request
        And this is the first attempt for this verify request
        Then status value stored in the EVS EmploymentVerificationRequest table is updated to 'Completed'
        And the result is stored in the EmploymentVerificationResponse table with Employed=False
        And EVS publishes a VerifyNotEmployedCompleted message with NotEmployed=True
        And processing ends

    Scenario: Process response - NotEmployed = false
        Given the employment check result has been received from the Employment Check service
        And the employment check request has completed successfully (no errors or exceptions)
        And the result returns employed = 'true' result (failure response)
        When the result is processed
        And the result is matched to the verify request
        Then status value stored in the EVS EmploymentVerificationRequest table is updated to 'Completed'
        And the result is stored in the EmploymentVerificationResponse table with Employed=True
        And EVS publishes a VerifyNotEmployedCompleted message with NotEmployed=False
