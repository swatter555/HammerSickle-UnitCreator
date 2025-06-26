using System;

namespace HammerSickle.UnitCreator.Models
{
    /// <summary>
    /// Represents the result of an operation with success status, message, and optional exception.
    /// Provides a consistent way to handle operation outcomes throughout the Unit Creator application.
    /// 
    /// Used for file operations, data validation, and other operations where success/failure
    /// status needs to be communicated along with descriptive messages and error details.
    /// </summary>
    public class OperationResult
    {
        private const string CLASS_NAME = nameof(OperationResult);

        /// <summary>
        /// Gets whether the operation completed successfully
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets a descriptive message about the operation result
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets the exception that caused the failure, if any
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Gets whether this result represents a failure
        /// </summary>
        public bool IsFailure => !Success;

        /// <summary>
        /// Gets whether this result has an associated exception
        /// </summary>
        public bool HasException => Exception != null;

        /// <summary>
        /// Private constructor to enforce factory method usage
        /// </summary>
        private OperationResult() { }

        /// <summary>
        /// Creates a successful operation result with an optional message
        /// </summary>
        /// <param name="message">Optional success message</param>
        /// <returns>Successful OperationResult</returns>
        public static OperationResult Successful(string message = "Operation completed successfully")
        {
            return new OperationResult
            {
                Success = true,
                Message = message ?? "Operation completed successfully"
            };
        }

        /// <summary>
        /// Creates a failed operation result with a descriptive message
        /// </summary>
        /// <param name="message">Error message describing the failure</param>
        /// <param name="exception">Optional exception that caused the failure</param>
        /// <returns>Failed OperationResult</returns>
        public static OperationResult Failed(string message, Exception? exception = null)
        {
            try
            {
                if (exception != null)
                {
                    HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(Failed), exception);
                }

                return new OperationResult
                {
                    Success = false,
                    Message = message ?? "Operation failed",
                    Exception = exception
                };
            }
            catch (Exception e)
            {
                // Fallback error handling if AppService fails
                return new OperationResult
                {
                    Success = false,
                    Message = $"Operation failed: {message}. Additional error in result creation: {e.Message}",
                    Exception = exception ?? e
                };
            }
        }

        /// <summary>
        /// Creates a failed operation result from an exception
        /// </summary>
        /// <param name="exception">Exception that caused the failure</param>
        /// <param name="contextMessage">Optional context message to prepend</param>
        /// <returns>Failed OperationResult</returns>
        public static OperationResult FromException(Exception exception, string? contextMessage = null)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            var message = string.IsNullOrWhiteSpace(contextMessage)
                ? exception.Message
                : $"{contextMessage}: {exception.Message}";

            return Failed(message, exception);
        }

        /// <summary>
        /// Creates a failed operation result for validation errors
        /// </summary>
        /// <param name="validationErrors">Collection of validation error messages</param>
        /// <returns>Failed OperationResult with validation context</returns>
        public static OperationResult ValidationFailed(System.Collections.Generic.IEnumerable<string> validationErrors)
        {
            if (validationErrors == null)
                return Failed("Validation failed: No error details provided");

            var errors = string.Join("; ", validationErrors);
            return Failed($"Validation failed: {errors}");
        }

        /// <summary>
        /// Creates a failed operation result for validation errors with count
        /// </summary>
        /// <param name="errorCount">Number of validation errors</param>
        /// <param name="firstError">First validation error message for context</param>
        /// <returns>Failed OperationResult with validation summary</returns>
        public static OperationResult ValidationFailed(int errorCount, string? firstError = null)
        {
            var message = errorCount == 1
                ? $"Validation failed: {firstError ?? "1 error found"}"
                : $"Validation failed: {errorCount} errors found" +
                  (string.IsNullOrWhiteSpace(firstError) ? "" : $". First error: {firstError}");

            return Failed(message);
        }

        /// <summary>
        /// Implicit conversion to bool for easy success checking
        /// </summary>
        /// <param name="result">OperationResult to convert</param>
        /// <returns>True if operation was successful, false otherwise</returns>
        public static implicit operator bool(OperationResult result)
        {
            return result?.Success ?? false;
        }

        /// <summary>
        /// Returns a string representation of the operation result
        /// </summary>
        /// <returns>Formatted string with success status and message</returns>
        public override string ToString()
        {
            var status = Success ? "SUCCESS" : "FAILED";
            var exceptionInfo = HasException ? $" (Exception: {Exception?.GetType().Name})" : "";
            return $"[{status}] {Message}{exceptionInfo}";
        }

        /// <summary>
        /// Gets the full error details including exception information
        /// </summary>
        /// <returns>Detailed error information for logging or debugging</returns>
        public string GetFullErrorDetails()
        {
            if (Success)
                return Message;

            var details = $"Error: {Message}";

            if (HasException)
            {
                details += $"\nException Type: {Exception!.GetType().FullName}";
                details += $"\nException Message: {Exception.Message}";

                if (!string.IsNullOrWhiteSpace(Exception.StackTrace))
                {
                    details += $"\nStack Trace:\n{Exception.StackTrace}";
                }
            }

            return details;
        }

        /// <summary>
        /// Combines two operation results, treating any failure as overall failure
        /// </summary>
        /// <param name="other">Other operation result to combine with</param>
        /// <returns>Combined operation result</returns>
        public OperationResult CombineWith(OperationResult other)
        {
            if (other == null)
                return this;

            if (Success && other.Success)
            {
                return Successful($"{Message}; {other.Message}");
            }

            // If either failed, combine error messages
            var combinedMessage = Success ? other.Message : $"{Message}; {other.Message}";
            var combinedException = Exception ?? other.Exception;

            return Failed(combinedMessage, combinedException);
        }
    }
}