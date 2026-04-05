using Application.DTOs.Common;
using System;

namespace Application.Exceptions
{
    public class AppException : Exception
    {
        public string ErrorCode { get; }
        public int StatusCode { get; }

        public AppException(string errorCode, string? customMessage = null, int statusCode = 400) 
            : base(customMessage ?? ErrorMessages.GetMessage(errorCode))
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
        }
    }
}
