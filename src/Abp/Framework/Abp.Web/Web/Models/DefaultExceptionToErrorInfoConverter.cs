﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Configuration;
using Abp.Runtime.Validation;
using Abp.UI;
using Abp.Utils.Extensions.Collections;
using Abp.Web.Localization;

namespace Abp.Web.Models
{
    //TODO@Halil: I did not like constructing ErrorInfo this way. It works wlll but I think we should change it later...
    internal class DefaultExceptionToErrorInfoConverter : IExceptionToErrorInfoConverter
    {
        public IExceptionToErrorInfoConverter Next { set; private get; }

        private static bool SendAllExceptionsToClients
        {
            get
            {
                return string.Equals(WebConfigurationManager.AppSettings["Abp.Web.SendAllExceptionsToClients"] ?? "false", "true", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public ErrorInfo Convert(Exception exception)
        {
            if (SendAllExceptionsToClients)
            {
                return CreateDetailedErrorInfoFromException(exception);
            }

            if (exception is AggregateException && exception.InnerException != null)
            {
                var aggException = exception as AggregateException;
                if (aggException.InnerException is UserFriendlyException || aggException.InnerException is AbpValidationException)
                {
                    exception = aggException.InnerException;
                }
            }

            if (exception is UserFriendlyException)
            {
                var userFriendlyException = exception as UserFriendlyException;
                return new ErrorInfo(userFriendlyException.Message, userFriendlyException.Details);
            }

            if (exception is AbpValidationException)
            {
                return new ErrorInfo(AbpWebLocalizedMessages.ValidationError)
                       {
                           ValidationErrors = GetValidationErrorInfos(exception as AbpValidationException)
                       };
            }

            return new ErrorInfo(AbpWebLocalizedMessages.InternalServerError);
        }

        private static ErrorInfo CreateDetailedErrorInfoFromException(Exception exception)
        {
            var detailBuilder = new StringBuilder();

            AddExceptionToDetails(exception, detailBuilder);

            var errorInfo = new ErrorInfo(exception.Message, detailBuilder.ToString());

            if (exception is AbpValidationException)
            {
                errorInfo.ValidationErrors = GetValidationErrorInfos(exception as AbpValidationException);
            }

            return errorInfo;
        }

        private static void AddExceptionToDetails(Exception exception, StringBuilder detailBuilder)
        {
            //Exception Message
            detailBuilder.AppendLine(exception.GetType().Name + ": " + exception.Message);

            //Additional info for UserFriendlyException
            if (exception is UserFriendlyException)
            {
                var userFriendlyException = exception as UserFriendlyException;
                if (!string.IsNullOrEmpty(userFriendlyException.Details))
                {
                    detailBuilder.AppendLine(userFriendlyException.Details);
                }
            }

            //Exception StackTrace
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                detailBuilder.AppendLine("STACK TRACE: " + exception.StackTrace);
            }

            //Inner exception
            if (exception.InnerException != null)
            {
                AddExceptionToDetails(exception.InnerException, detailBuilder);
            }

            //Inner exceptions for AggregateException
            if (exception is AggregateException)
            {
                var aggException = exception as AggregateException;
                if (aggException.InnerExceptions.IsNullOrEmpty())
                {
                    return;
                }

                foreach (var innerException in aggException.InnerExceptions)
                {
                    AddExceptionToDetails(innerException, detailBuilder);
                }
            }
        }

        private static ValidationErrorInfo[] GetValidationErrorInfos(AbpValidationException validationException)
        {
            var validationErrorInfos = new List<ValidationErrorInfo>();

            foreach (var validationResult in validationException.ValidationErrors)
            {
                var validationError = new ValidationErrorInfo(validationResult.ErrorMessage);

                if (validationResult.MemberNames != null && validationResult.MemberNames.Any())
                {
                    validationError.Members = validationResult.MemberNames.ToArray();
                }

                validationErrorInfos.Add(validationError);
            }

            return validationErrorInfos.ToArray();
        }
    }
}