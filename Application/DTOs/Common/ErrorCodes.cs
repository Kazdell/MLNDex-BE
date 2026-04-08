using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Common
{
	public static class ErrorCodes
	{
		//  Validation
		public const string VALIDATION_ERROR = "VALIDATION_ERROR";
		public const string INVALID_INPUT = "INVALID_INPUT";

		//  Auth
		public const string UNAUTHORIZED = "UNAUTHORIZED";
		public const string FORBIDDEN = "FORBIDDEN";
		public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
		public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";
		public const string INVALID_TOKEN = "INVALID_TOKEN";

		//  User
		public const string USER_NOT_FOUND = "USER_NOT_FOUND";
		public const string USER_ALREADY_EXISTS = "USER_ALREADY_EXISTS";

		//  Data & Resource
		public const string NOT_FOUND = "NOT_FOUND";
		public const string ALREADY_EXISTS = "ALREADY_EXISTS";
		public const string CONFLICT = "CONFLICT";
		public const string DB_ERROR = "DB_ERROR";

		//  Server
		public const string INTERNAL_SERVER_ERROR = "INTERNAL_SERVER_ERROR";
		public const string BAD_REQUEST = "BAD_REQUEST";
		public const string SERVICE_UNAVAILABLE = "SERVICE_UNAVAILABLE";
		public const string TIMEOUT = "TIMEOUT";

    //  Business logic
    public const string INSUFFICIENT_BALANCE = "INSUFFICIENT_BALANCE";
    public const string OPERATION_NOT_ALLOWED = "OPERATION_NOT_ALLOWED";

    //  Payment & Financial
    public const string COIN_PACKAGE_NOT_FOUND = "COIN_PACKAGE_NOT_FOUND";
    public const string INVALID_WITHDRAWAL_AMOUNT = "INVALID_WITHDRAWAL_AMOUNT";
    public const string WITHDRAWAL_NOT_FOUND = "WITHDRAWAL_NOT_FOUND";

    public const string CHAPTER_PRICE_NOT_CONFIGURED = "CHAPTER_PRICE_NOT_CONFIGURED";
    public const string INVALID_TRANSLATION_CHAPTER = "INVALID_TRANSLATION_CHAPTER";
    public const string ORIGINAL_CHAPTER_FREE = "ORIGINAL_CHAPTER_FREE";
    public const string ORIGINAL_CHAPTER_UNLOCKED = "ORIGINAL_CHAPTER_UNLOCKED";
    public const string TRANSLATION_ALREADY_UNLOCKED = "TRANSLATION_ALREADY_UNLOCKED";
    public const string TEAM_MONETIZATION_DISABLED = "TEAM_MONETIZATION_DISABLED";
    public const string TRANSLATION_PRICE_NOT_CONFIGURED = "TRANSLATION_PRICE_NOT_CONFIGURED";
    public const string INVALID_UNLOCK_PRICE = "INVALID_UNLOCK_PRICE";

    //  Moderation & Report
    public const string REPORT_NOT_FOUND = "REPORT_NOT_FOUND";
    public const string MODERATION_QUEUE_NOT_FOUND = "MODERATION_QUEUE_NOT_FOUND";
    public const string MODERATOR_NOT_FOUND = "MODERATOR_NOT_FOUND";
    public const string INVALID_MODERATOR_ACTION = "INVALID_MODERATOR_ACTION";
    public const string APPEAL_NOT_FOUND = "APPEAL_NOT_FOUND";

    //  System
    public const string INVALID_CONFIG_VALUE = "INVALID_CONFIG_VALUE";
    public const string SYSTEM_CONFIG_NOT_FOUND = "SYSTEM_CONFIG_NOT_FOUND";

    //  Translation & Teams
    public const string TEAM_NOT_FOUND = "TEAM_NOT_FOUND";
    public const string NOT_TEAM_MEMBER = "NOT_TEAM_MEMBER";
    public const string INVITATION_NOT_FOUND = "INVITATION_NOT_FOUND";
    public const string DUPLICATE_TRANSLATION_TEAM = "DUPLICATE_TRANSLATION_TEAM";
    public const string TRANSLATION_NOT_FOUND = "TRANSLATION_NOT_FOUND";
    public const string CHAPTER_ALREADY_UNLOCKED = "CHAPTER_ALREADY_UNLOCKED";
    public const string CHAPTER_LOCKED = "CHAPTER_LOCKED";

    public const string TRANSLATION_PERMISSION_NOT_FOUND = "TRANSLATION_PERMISSION_NOT_FOUND";
    public const string LANGUAGE_MISMATCH = "LANGUAGE_MISMATCH";
    public const string PERMISSION_NOT_VALID_FOR_SERIES = "PERMISSION_NOT_VALID_FOR_SERIES";
    public const string TEAM_ID_REQUIRED_UNOFFICIAL = "TEAM_ID_REQUIRED_UNOFFICIAL";
    public const string TRANSLATION_RETRIEVE_FAILED = "TRANSLATION_RETRIEVE_FAILED";
    public const string MISSING_TRANSLATION_PERMISSION = "MISSING_TRANSLATION_PERMISSION";
    public const string UNAUTHORIZED_EDIT = "UNAUTHORIZED_EDIT";
    public const string UNAUTHORIZED_DELETE = "UNAUTHORIZED_DELETE";
    public const string TRANSLATION_NOT_FOUND_OR_NOT_OWNER = "TRANSLATION_NOT_FOUND_OR_NOT_OWNER";
    public const string LANGUAGE_NOT_FOUND = "LANGUAGE_NOT_FOUND";
    public const string PERMISSION_REQUEST_PENDING = "PERMISSION_REQUEST_PENDING";
    public const string PERMISSION_ALREADY_GRANTED = "PERMISSION_ALREADY_GRANTED";
    public const string PERMISSION_REQUEST_NOT_FOUND = "PERMISSION_REQUEST_NOT_FOUND";
    public const string CREATOR_ONLY_REVIEW = "CREATOR_ONLY_REVIEW";
    public const string TEAM_MEMBER_ONLY_VIEW = "TEAM_MEMBER_ONLY_VIEW";

    //  Creator & Series
    public const string SERIES_NOT_FOUND = "SERIES_NOT_FOUND";
    public const string CHAPTER_NOT_FOUND = "CHAPTER_NOT_FOUND";
    public const string DUPLICATE_SERIES_TITLE = "DUPLICATE_SERIES_TITLE";
    public const string PROHIBITED_CONTENT = "PROHIBITED_CONTENT";

    public const string CHAPTER_NOT_PUBLISHED = "CHAPTER_NOT_PUBLISHED";
    public const string CHAPTER_ALREADY_FREE = "CHAPTER_ALREADY_FREE";
    public const string CREATOR_NOT_FOUND = "CREATOR_NOT_FOUND";

    //  VIP
    public const string WALLET_NOT_FOUND = "WALLET_NOT_FOUND";
    public const string SUBSCRIPTION_NOT_FOUND = "SUBSCRIPTION_NOT_FOUND";
    public const string VIP_PACKAGE_NOT_FOUND = "VIP_PACKAGE_NOT_FOUND";
    public const string INVALID_TRANSACTION = "INVALID_TRANSACTION";

  }
}
