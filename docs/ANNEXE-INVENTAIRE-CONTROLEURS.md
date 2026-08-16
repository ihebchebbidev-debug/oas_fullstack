# Annexe — Inventaire exhaustif des contrôleurs backend

> Généré par `scripts/inventory_controllers.py` (analyse statique de `Backend/**/Controllers/*.cs`).
> Aucune route inférée : uniquement le texte littéral des attributs. Régénérer avec :
> `python3 scripts/inventory_controllers.py --markdown`

- Fichiers contrôleurs : **95**
- Actions HTTP (méthodes portant au moins un attribut `[Http*]`) : **993**
- Mappings de route (attributs `[Http*]`, une méthode pouvant en porter plusieurs) : **996**
- Modules : **47**

## Récapitulatif par module

| Module | Fichiers | Actions |
|---|---:|---:|
| AiChat | 2 | 14 |
| Articles | 4 | 31 |
| Auth | 4 | 32 |
| Calendar | 1 | 17 |
| Contacts | 4 | 24 |
| Dashboards | 4 | 18 |
| Deals | 1 | 13 |
| Dispatches | 1 | 31 |
| Documents | 1 | 8 |
| DynamicForms | 2 | 12 |
| EmailAccounts | 3 | 30 |
| ExternalEndpoints | 2 | 16 |
| HR | 1 | 73 |
| Incidents | 1 | 1 |
| Installations | 2 | 14 |
| Invoices | 1 | 11 |
| Lookups | 2 | 124 |
| ModuleRequests | 1 | 1 |
| Notifications | 1 | 8 |
| Numbering | 1 | 6 |
| Offers | 1 | 16 |
| OfflineHydration | 1 | 2 |
| Payments | 1 | 30 |
| Planning | 2 | 18 |
| PlanningProfiles | 1 | 7 |
| Plugins | 2 | 13 |
| Preferences | 2 | 12 |
| Processes | 1 | 10 |
| Projects | 8 | 111 |
| Purchases | 5 | 37 |
| Reporting | 1 | 5 |
| RetenueSource | 1 | 9 |
| Roles | 2 | 19 |
| Sales | 1 | 12 |
| ServiceOrders | 1 | 33 |
| Settings | 2 | 7 |
| Shared | 6 | 24 |
| Signatures | 1 | 3 |
| Skills | 1 | 12 |
| SupportTickets | 2 | 16 |
| Sync | 1 | 4 |
| Tenants | 1 | 7 |
| UserAiSettings | 1 | 7 |
| UserGroups | 1 | 9 |
| Users | 1 | 12 |
| WebsiteBuilder | 4 | 46 |
| WorkflowEngine | 5 | 28 |
| **TOTAL** | **95** | **993** |

## Détail par contrôleur

### AiChat

#### `Backend/Modules/AiChat/Controllers/AiChatController.cs` — `AiChatController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 12

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 29 | GET | `conversations` | `GetConversations` | — |
| 51 | GET | `conversations/{id}` | `GetConversation` | — |
| 97 | POST | `conversations` | `CreateConversation` | — |
| 116 | PATCH | `conversations/{id}` | `UpdateConversation` | — |
| 143 | DELETE | `conversations/{id}` | `DeleteConversation` | — |
| 168 | DELETE | `conversations` | `DeleteAllConversations` | — |
| 187 | POST | `conversations/{id}/archive` | `ArchiveConversation` | — |
| 212 | POST | `conversations/{id}/pin` | `PinConversation` | — |
| 241 | POST | `messages` | `AddMessage` | — |
| 269 | POST | `messages/bulk` | `AddMessages` | — |
| 297 | PATCH | `messages/{id}/feedback` | `UpdateMessageFeedback` | — |
| 324 | DELETE | `messages/{id}` | `DeleteMessage` | — |

#### `Backend/Modules/AiChat/Controllers/GenerateWishController.cs` — `GenerateWishController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 2

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 37 | POST | `(vide)` | `Generate` | AllowAnonymous |
| 115 | POST | `stream` | `Stream` | AllowAnonymous |

### Articles

#### `Backend/Modules/Articles/Controllers/ArticleGroupsController.cs` — `ArticleGroupsController`

- Route de classe : `api/articles/groups`
- Autorisation de classe : Authorize
- Actions : 5

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 53 | GET | `(vide)` | `GetArticleGroups` | — |
| 72 | GET | `{id}` | `GetArticleGroupById` | — |
| 93 | POST | `(vide)` | `CreateArticleGroup` | — |
| 126 | PUT | `{id}` | `UpdateArticleGroup` | — |
| 162 | DELETE | `{id}` | `DeleteArticleGroup` | — |

#### `Backend/Modules/Articles/Controllers/ArticleNotesController.cs` — `ArticleNotesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 5

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 24 | GET | `article/{articleId}` | `GetByArticleId` | — |
| 39 | GET | `{id}` | `GetNote` | — |
| 47 | POST | `(vide)` | `Create` | — |
| 64 | PUT | `{id}` | `Update` | — |
| 73 | DELETE | `{id}` | `Delete` | — |

#### `Backend/Modules/Articles/Controllers/ArticlesController.cs` — `ArticlesController`

- Route de classe : `api/articles`
- Autorisation de classe : Authorize
- Actions : 14

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 51 | GET | `(vide)` | `GetAllArticles` | — |
| 72 | GET | `{id}` | `GetArticleById` | — |
| 86 | POST | `(vide)` | `CreateArticle` | — |
| 121 | PUT | `{id}` | `UpdateArticle` | — |
| 167 | DELETE | `{id}` | `DeleteArticle` | — |
| 188 | POST | `transactions` | `CreateTransaction` | — |
| 223 | GET | `{articleId}/transactions` | `GetArticleTransactions` | — |
| 235 | GET | `transactions` | `GetAllTransactions` | — |
| 249 | POST | `batch` | `BatchUpdateStock` | — |
| 288 | GET | `categories` | `GetAllCategories` | — |
| 299 | POST | `categories` | `CreateCategory` | — |
| 316 | GET | `locations` | `GetAllLocations` | — |
| 327 | POST | `locations` | `CreateLocation` | — |
| 345 | POST | `import` | `BulkImportArticles` | — |

#### `Backend/Modules/Articles/Controllers/StockTransactionController.cs` — `StockTransactionController`

- Route de classe : `api/stock-transactions`
- Autorisation de classe : Authorize
- Actions : 7

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 28 | GET | `(vide)` | `GetTransactions` | — |
| 70 | GET | `article/{articleId}` | `GetArticleTransactions` | — |
| 90 | GET | `{id}` | `GetTransaction` | — |
| 111 | POST | `add` | `AddStock` | — |
| 157 | POST | `remove` | `RemoveStock` | — |
| 207 | POST | `deduct-from-sale/{saleId}` | `DeductStockFromSale` | — |
| 237 | POST | `restore-from-sale/{saleId}` | `RestoreStockFromSale` | — |

### Auth

#### `Backend/Modules/Auth/Controllers/AuthController.cs` — `AuthController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 25

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 45 | GET | `admin-exists` | `CheckAdminExists` | — |
| 72 | GET | `oauth-config/{provider}` | `GetOAuthConfig` | AllowAnonymous |
| 125 | POST | `login` | `Login` | — |
| 188 | POST | `user-login` | `UserLogin` | — |
| 250 | POST | `signup` | `Signup` | — |
| 297 | POST | `oauth-login` | `OAuthLogin` | — |
| 339 | POST | `forgot-password` | `ForgotPassword` | AllowAnonymous |
| 385 | POST | `check-email-exists` | `CheckEmailExists` | AllowAnonymous |
| 417 | POST | `verify-otp` | `VerifyOtp` | AllowAnonymous |
| 475 | POST | `reset-password` | `ResetPassword` | AllowAnonymous |
| 532 | POST | `refresh` | `RefreshToken` | — |
| 572 | GET | `user/{userId}` | `GetUser` | Authorize |
| 602 | GET | `admin-users` | `GetAllAdminUsers` | Authorize |
| 623 | GET | `me` | `GetCurrentUser` | Authorize |
| 656 | PUT | `user/{userId}` | `UpdateUser` | Authorize |
| 695 | PUT | `user/{userId}/profile-picture` | `UpdateUserProfilePicture` | Authorize |
| 725 | PUT | `me/profile-picture` | `UpdateMyProfilePicture` | Authorize |
| 758 | PUT | `me` | `UpdateCurrentUser` | Authorize |
| 799 | GET | `company-logo` | `GetCompanyLogo` | AllowAnonymous |
| 839 | GET | `company-logo-base64` | `GetCompanyLogoBase64` | AllowAnonymous |
| 917 | POST | `change-password` | `ChangePassword` | Authorize |
| 967 | POST | `logout` | `Logout` | Authorize |
| 1022 | GET | `status` | `GetAuthStatus` | Authorize |
| 1050 | GET | `test-db` | `TestDatabase` | — |
| 1083 | POST | `test-signup` | `TestSignup` | — |

#### `Backend/Modules/Auth/Controllers/EmailVerificationController.cs` — `EmailVerificationController`

- Route de classe : `api/email-verification`
- Autorisation de classe : Authorize
- Actions : 3

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 36 | GET | `status` | `Status` | — |
| 51 | POST | `request` | `RequestCode` | — |
| 76 | POST | `verify` | `Verify` | — |

#### `Backend/Modules/Auth/Controllers/OAuthCallbackController.cs` — `OAuthCallbackController`

- Route de classe : `oauth`
- Autorisation de classe : AllowAnonymous
- Actions : 2

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 54 | GET | `google/callback` | `GoogleCallback` | — |
| 131 | GET | `microsoft/callback` | `MicrosoftCallback` | — |

#### `Backend/Modules/Auth/Controllers/TwoFactorController.cs` — `TwoFactorController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 2

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 42 | POST | `verify` | `Verify` | — |
| 108 | POST | `resend` | `Resend` | — |

### Calendar

#### `Backend/Modules/Calendar/Controllers/CalendarController.cs` — `CalendarController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 17

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 20 | GET | `events` | `GetAllEvents` | — |
| 27 | GET | `events/{id}` | `GetEvent` | — |
| 37 | GET | `events/date-range` | `GetEventsByDateRange` | — |
| 46 | GET | `events/contact/{contactId}` | `GetEventsByContact` | — |
| 53 | POST | `events` | `CreateEvent` | — |
| 63 | PUT | `events/{id}` | `UpdateEvent` | — |
| 76 | DELETE | `events/{id}` | `DeleteEvent` | — |
| 87 | GET | `event-types` | `GetAllEventTypes` | — |
| 94 | GET | `event-types/{id}` | `GetEventType` | — |
| 104 | POST | `event-types` | `CreateEventType` | — |
| 114 | DELETE | `event-types/{id}` | `DeleteEventType` | — |
| 125 | GET | `events/{eventId}/attendees` | `GetEventAttendees` | — |
| 132 | POST | `events/attendees` | `CreateEventAttendee` | — |
| 142 | DELETE | `events/attendees/{id}` | `DeleteEventAttendee` | — |
| 153 | GET | `events/{eventId}/reminders` | `GetEventReminders` | — |
| 160 | POST | `events/reminders` | `CreateEventReminder` | — |
| 170 | DELETE | `events/reminders/{id}` | `DeleteEventReminder` | — |

### Contacts

#### `Backend/Modules/Contacts/Controllers/ContactActivitiesController.cs` — `ContactActivitiesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 1

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 27 | GET | `contact/{contactId}` | `GetByContactId` | — |

#### `Backend/Modules/Contacts/Controllers/ContactNotesController.cs` — `ContactNotesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 5

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 27 | GET | `contact/{contactId}` | `GetNotesByContactId` | — |
| 45 | GET | `{id}` | `GetNote` | — |
| 69 | POST | `(vide)` | `CreateNote` | — |
| 99 | PUT | `{id}` | `UpdateNote` | — |
| 128 | DELETE | `{id}` | `DeleteNote` | — |

#### `Backend/Modules/Contacts/Controllers/ContactTagsController.cs` — `ContactTagsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 6

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 26 | GET | `(vide)` | `GetAllTags` | — |
| 44 | GET | `{id}` | `GetTag` | — |
| 68 | POST | `(vide)` | `CreateTag` | — |
| 97 | PUT | `{id}` | `UpdateTag` | — |
| 131 | DELETE | `{id}` | `DeleteTag` | — |
| 155 | GET | `exists/{name}` | `TagExists` | — |

#### `Backend/Modules/Contacts/Controllers/ContactsController.cs` — `ContactsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 12

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 33 | GET | `(vide)` | `GetAllContacts` | — |
| 52 | GET | `{id}` | `GetContact` | — |
| 77 | POST | `(vide)` | `CreateContact` | — |
| 111 | PUT | `{id}` | `UpdateContact` | — |
| 150 | DELETE | `{id}` | `DeleteContact` | — |
| 178 | GET | `search` | `SearchContacts` | — |
| 199 | GET | `exists/{email}` | `ContactExists` | — |
| 217 | POST | `import` | `BulkImportContacts` | — |
| 245 | POST | `{contactId}/tags/{tagId}` | `AssignTagToContact` | — |
| 273 | DELETE | `{contactId}/tags/{tagId}` | `RemoveTagFromContact` | — |
| 300 | POST | `{contactId}/user-groups/{groupId}` | `AssignUserGroupToContact` | — |
| 326 | DELETE | `{contactId}/user-groups/{groupId}` | `RemoveUserGroupFromContact` | — |

### Dashboards

#### `Backend/Modules/Dashboards/Controllers/DashboardLayoutController.cs` — `DashboardLayoutController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 3

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 42 | GET | `(vide)` | `Get` | — |
| 61 | PUT | `(vide)` | `Save` | — |
| 88 | DELETE | `(vide)` | `Reset` | — |

#### `Backend/Modules/Dashboards/Controllers/DashboardsController.cs` — `DashboardsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 9

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 59 | GET | `(vide)` | `GetAll` | — |
| 73 | GET | `{id:int}` | `GetById` | — |
| 88 | POST | `(vide)` | `Create` | — |
| 118 | PUT | `{id:int}` | `Update` | — |
| 145 | DELETE | `{id:int}` | `Delete` | — |
| 163 | POST | `{id:int}/duplicate` | `Duplicate` | — |
| 195 | POST | `{id:int}/share` | `GenerateShareLink` | — |
| 234 | DELETE | `{id:int}/share` | `RevokeShareLink` | — |
| 260 | GET | `public/{token}` | `GetByShareToken` | AllowAnonymous |

#### `Backend/Modules/Dashboards/Controllers/ExternalApiProxyController.cs` — `ExternalApiProxyController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 1

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 41 | POST | `fetch` | `FetchExternal` | — |

#### `Backend/Modules/Dashboards/Controllers/ReportingController.cs` — `ReportingController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 5

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 51 | GET | `sales` | `GetSalesReport` | — |
| 185 | GET | `service` | `GetServiceReport` | — |
| 271 | GET | `finance` | `GetFinanceReport` | — |
| 347 | GET | `hr` | `GetHrReport` | — |
| 462 | GET | `purchase` | `GetPurchaseReport` | — |

### Deals

#### `Backend/Modules/Deals/Controllers/DealsController.cs` — `DealsController`

- Route de classe : `api/deals`
- Autorisation de classe : Authorize
- Actions : 13

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 33 | GET | `(vide)` | `GetDeals` | — |
| 58 | GET | `stats` | `GetStats` | — |
| 73 | GET | `{id:int}` | `GetDealById` | — |
| 89 | POST | `(vide)` | `CreateDeal` | — |
| 109 | PATCH | `{id:int}` | `UpdateDeal` | — |
| 129 | DELETE | `{id:int}` | `DeleteDeal` | — |
| 145 | POST | `{id:int}/convert` | `ConvertDeal` | — |
| 170 | POST | `{id:int}/items` | `AddItem` | — |
| 190 | PATCH | `{id:int}/items/{itemId:int}` | `UpdateItem` | — |
| 210 | DELETE | `{id:int}/items/{itemId:int}` | `DeleteItem` | — |
| 228 | GET | `{id:int}/activities` | `GetActivities` | — |
| 243 | POST | `{id:int}/activities` | `AddActivity` | — |
| 263 | DELETE | `{id:int}/activities/{activityId:int}` | `DeleteActivity` | — |

### Dispatches

#### `Backend/Modules/Dispatches/Controllers/DispatchesController.cs` — `DispatchesController`

- Route de classe : `api/dispatches`
- Autorisation de classe : Authorize
- Actions : 31

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 35 | POST | `from-job/{jobId:int}` | `CreateFromJob` | — |
| 47 | POST | `from-installation` | `CreateFromInstallation` | — |
| 61 | POST | `from-service-order` | `CreateFromServiceOrder` | — |
| 77 | POST | `installations/{installationId:int}/jobs` | `AddJobsToInstallationDispatch` | — |
| 107 | GET | `(vide)` | `GetAll` | — |
| 114 | GET | `{dispatchId:int}` | `GetById` | — |
| 121 | PUT | `{dispatchId:int}` | `Update` | — |
| 133 | PATCH | `{dispatchId:int}/status` | `UpdateStatus` | — |
| 144 | GET | `{dispatchId:int}/audit-logs` | `GetAuditLogs` | — |
| 151 | POST | `{dispatchId:int}/start` | `Start` | — |
| 162 | POST | `{dispatchId:int}/complete` | `Complete` | — |
| 173 | DELETE | `{dispatchId:int}` | `Delete` | — |
| 184 | POST | `{dispatchId:int}/time-entries` | `AddTimeEntry` | — |
| 196 | GET | `{dispatchId:int}/time-entries` | `GetTimeEntries` | — |
| 203 | POST | `{dispatchId:int}/time-entries/{timeEntryId:int}/approve` | `ApproveTimeEntry` | — |
| 214 | PUT | `{dispatchId:int}/time-entries/{timeEntryId:int}` | `UpdateTimeEntry` | — |
| 225 | DELETE | `{dispatchId:int}/time-entries/{timeEntryId:int}` | `DeleteTimeEntry` | — |
| 236 | POST | `{dispatchId:int}/expenses` | `AddExpense` | — |
| 248 | GET | `{dispatchId:int}/expenses` | `GetExpenses` | — |
| 255 | POST | `{dispatchId:int}/expenses/{expenseId:int}/approve` | `ApproveExpense` | — |
| 266 | PUT | `{dispatchId:int}/expenses/{expenseId:int}` | `UpdateExpense` | — |
| 277 | DELETE | `{dispatchId:int}/expenses/{expenseId:int}` | `DeleteExpense` | — |
| 288 | POST | `{dispatchId:int}/materials` | `AddMaterial` | — |
| 300 | GET | `{dispatchId:int}/materials` | `GetMaterials` | — |
| 307 | POST | `{dispatchId:int}/materials/{materialId:int}/approve` | `ApproveMaterial` | — |
| 319 | POST | `{dispatchId:int}/attachments` | `UploadAttachment` | — |
| 332 | POST | `{dispatchId:int}/notes` | `AddNote` | — |
| 345 | GET | `{dispatchId:int}/notes` | `GetNotes` | — |
| 354 | GET | `{dispatchId:int}/history` | `GetActivityLog` | — |
| 363 | POST | `{dispatchId:int}/history` | `LogActivity` | — |
| 369 | GET | `statistics` | `GetStatistics` | — |

### Documents

#### `Backend/Modules/Documents/Controllers/DocumentsController.cs` — `DocumentsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 8

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 258 | GET | `(vide)` | `GetDocuments` | — |
| 318 | GET | `stats` | `GetStats` | — |
| 377 | GET | `{id}` | `GetDocument` | — |
| 390 | POST | `(vide)` | `CreateDocument` | — |
| 459 | POST | `upload` | `UploadDocuments` | — |
| 596 | GET | `download/{id}` | `DownloadDocument` | — |
| 645 | DELETE | `{id}` | `DeleteDocument` | — |
| 680 | POST | `bulk-delete` | `BulkDeleteDocuments` | — |

### DynamicForms

#### `Backend/Modules/DynamicForms/Controllers/DynamicFormsController.cs` — `DynamicFormsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 10

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 33 | GET | `(vide)` | `GetAll` | — |
| 52 | GET | `{id}` | `GetById` | — |
| 75 | POST | `(vide)` | `Create` | RequirePermission(dynamic_forms, create) |
| 95 | PUT | `{id}` | `Update` | RequirePermission(dynamic_forms, update) |
| 123 | DELETE | `{id}` | `Delete` | RequirePermission(dynamic_forms, delete) |
| 146 | POST | `{id}/duplicate` | `Duplicate` | RequirePermission(dynamic_forms, create) |
| 170 | POST | `{id}/status` | `ChangeStatus` | RequirePermission(dynamic_forms, update) |
| 197 | GET | `{id}/responses` | `GetResponses` | — |
| 216 | POST | `{id}/responses` | `SubmitResponse` | — |
| 243 | GET | `{id}/responses/count` | `GetResponseCount` | — |

#### `Backend/Modules/DynamicForms/Controllers/PublicFormsController.cs` — `PublicFormsController`

- Route de classe : `api/public/forms`
- Autorisation de classe : —
- Actions : 2

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 29 | GET | `{slug}` | `GetBySlug` | — |
| 51 | POST | `{slug}/responses` | `SubmitResponse` | — |

### EmailAccounts

#### `Backend/Modules/EmailAccounts/Controllers/CustomEmailController.cs` — `CustomEmailController`

- Route de classe : `api/email-accounts/custom`
- Autorisation de classe : Authorize
- Actions : 9

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 31 | POST | `test` | `TestConnection` | — |
| 84 | POST | `send` | `Send` | — |
| 120 | POST | `fetch` | `Fetch` | — |
| 186 | GET | `(vide)` | `List` | — |
| 203 | GET | `{id}` | `Get` | — |
| 212 | POST | `save` | `Save` | — |
| 220 | PUT | `{id}` | `Update` | — |
| 229 | DELETE | `{id}` | `Delete` | — |
| 238 | POST | `{id}/sync` | `Sync` | — |

#### `Backend/Modules/EmailAccounts/Controllers/EmailAccountsController.cs` — `EmailAccountsController`

- Route de classe : `api/email-accounts`
- Autorisation de classe : Authorize
- Actions : 11

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 38 | GET | `oauth-config/{provider}` | `GetOAuthConfig` | — |
| 60 | POST | `oauth-callback` | `OAuthCallback` | — |
| 88 | GET | `(vide)` | `GetMyAccounts` | — |
| 101 | GET | `{id}` | `GetAccount` | — |
| 112 | DELETE | `{id}` | `DisconnectAccount` | — |
| 126 | POST | `{id}/reconnect` | `ReconnectAccount` | — |
| 142 | PATCH | `{id}/email-settings` | `UpdateEmailSettings` | — |
| 158 | PATCH | `{id}/calendar-settings` | `UpdateCalendarSettings` | — |
| 174 | GET | `{id}/blocklist` | `GetBlocklist` | — |
| 187 | POST | `{id}/blocklist` | `AddBlocklistItem` | — |
| 201 | DELETE | `{id}/blocklist/{itemId}` | `RemoveBlocklistItem` | — |

#### `Backend/Modules/EmailAccounts/Controllers/EmailAccountsController_SyncEndpoints.cs` — `EmailAccountsController`

- Route de classe : `(aucun [Route] de classe)`
- Autorisation de classe : —
- Actions : 10

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 14 | POST | `{id}/sync-emails` | `SyncEmails` | — |
| 40 | GET | `{id}/emails` | `GetSyncedEmails` | — |
| 59 | POST | `{id}/sync-calendar` | `SyncCalendar` | — |
| 85 | GET | `{id}/calendar-events` | `GetCalendarEvents` | — |
| 104 | POST | `{id}/calendar-events` | `CreateCalendarEvent` | — |
| 129 | POST | `{id}/send-email` | `SendEmail` | — |
| 158 | PATCH | `{id}/emails/{emailId}/star` | `ToggleStarEmail` | — |
| 186 | PATCH | `{id}/emails/{emailId}/read` | `ToggleReadEmail` | — |
| 214 | DELETE | `{id}/emails/{emailId}` | `DeleteEmail` | — |
| 242 | GET | `{id}/emails/{emailId}/attachments/{attachmentId}` | `DownloadAttachment` | — |

### ExternalEndpoints

#### `Backend/Modules/ExternalEndpoints/Controllers/ExternalEndpointsController.cs` — `ExternalEndpointsController`

- Route de classe : `api/external-endpoints`
- Autorisation de classe : Authorize
- Actions : 14

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 30 | GET | `(vide)` | `GetEndpoints` | — |
| 45 | GET | `stats` | `GetStats` | — |
| 60 | GET | `{id:int}` | `GetById` | — |
| 76 | POST | `(vide)` | `Create` | — |
| 97 | PATCH | `{id:int}` | `Update` | — |
| 122 | DELETE | `{id:int}` | `Delete` | — |
| 139 | POST | `{id:int}/regenerate-key` | `RegenerateKey` | — |
| 174 | GET | `{id:int}/reveal-key` | `RevealKey` | — |
| 200 | GET | `{id:int}/logs` | `GetLogs` | — |
| 215 | GET | `{id:int}/logs/{logId:int}` | `GetLog` | — |
| 235 | GET | `{id:int}/logs/{logId:int}/convert-preview` | `ConvertPreview` | — |
| 251 | DELETE | `{id:int}/logs/{logId:int}` | `DeleteLog` | — |
| 267 | DELETE | `{id:int}/logs` | `ClearLogs` | — |
| 282 | PATCH | `{id:int}/logs/{logId:int}/read` | `MarkAsRead` | — |

#### `Backend/Modules/ExternalEndpoints/Controllers/ExternalReceiveController.cs` — `ExternalReceiveController`

- Route de classe : `api/external-receive`
- Autorisation de classe : AllowAnonymous
- Actions : 2

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 47 | OPTIONS | `{slug}` | `Preflight` | — |
| 76 | POST / GET / PUT | `{slug} / {slug} / {slug}` | `Receive` | — |

### HR

#### `Backend/Modules/HR/Controllers/HrController.cs` — `HrController`

- Route de classe : `api/hr`
- Autorisation de classe : Authorize
- Actions : 73

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 23 | GET | `employees` | `GetEmployeesAsync` | — |
| 26 | GET | `employees/{id:int}` | `GetEmployeeDetailAsync` | — |
| 29 | PUT | `employees/{userId:int}/salary-config` | `UpsertSalaryConfig` | — |
| 33 | GET | `employees/{userId:int}/salary-history` | `GetSalaryHistory` | — |
| 38 | GET | `leaves/balances` | `GetLeaveBalances` | — |
| 42 | PUT | `leaves/balances/{userId:int}` | `SetLeaveAllowance` | — |
| 47 | GET | `attendance` | `GetAttendance` | — |
| 51 | POST | `attendance` | `UpsertAttendance` | — |
| 64 | DELETE | `attendance/{id:int}` | `DeleteAttendance` | — |
| 71 | GET | `attendance/settings` | `GetAttendanceSettings` | — |
| 75 | PUT | `attendance/settings` | `UpsertAttendanceSettings` | — |
| 79 | POST | `attendance/import` | `ImportAttendance` | — |
| 84 | POST | `payroll/run` | `GenerateRun` | — |
| 88 | GET | `payroll/runs` | `ListRuns` | — |
| 92 | GET | `payroll/runs/{id:int}` | `GetPayrollRunAsync` | — |
| 95 | PUT | `payroll/runs/{id:int}/confirm` | `ConfirmRun` | — |
| 103 | PUT | `payroll/runs/{id:int}/pay` | `MarkRunPaid` | — |
| 111 | GET | `payroll/payslip/{entryId:int}` | `GetPayslipAsync` | — |
| 115 | GET | `departments` | `GetDepartmentsAsync` | — |
| 118 | POST | `departments` | `CreateDepartmentAsync` | — |
| 121 | PUT | `departments/{id:int}` | `UpdateDepartmentAsync` | — |
| 124 | DELETE | `departments/{id:int}` | `Ok` | — |
| 128 | GET | `bonuses` | `GetBonuses` | — |
| 132 | POST | `bonuses` | `CreateBonus` | — |
| 139 | PUT | `bonuses/{id:int}` | `UpdateBonus` | — |
| 146 | DELETE | `bonuses/{id:int}` | `Ok` | — |
| 150 | GET | `cnss/rates` | `GetCnssRatesAsync` | — |
| 153 | GET | `cnss/rates/active` | `GetActiveCnssRateAsync` | — |
| 156 | PUT | `cnss/rates` | `UpsertCnssRate` | — |
| 160 | GET | `cnss/declaration` | `GetDeclaration` | — |
| 165 | GET | `holidays` | `GetHolidays` | — |
| 169 | POST | `holidays` | `CreateHoliday` | — |
| 173 | PUT | `holidays/{id:int}` | `UpdateHoliday` | — |
| 177 | DELETE | `holidays/{id:int}` | `Ok` | — |
| 181 | GET | `documents/{userId:int}` | `GetEmployeeDocumentsAsync` | — |
| 184 | POST | `documents` | `AddDocument` | — |
| 188 | DELETE | `documents/{id:int}` | `Ok` | — |
| 192 | GET | `audit` | `GetAudit` | — |
| 197 | GET | `reports/employee-cost` | `EmployeeCost` | — |
| 202 | GET | `leaves/active` | `GetActiveLeaves` | — |
| 206 | GET | `contracts/expiring` | `GetExpiringContracts` | — |
| 213 | GET | `performance/goals` | `GetGoals` | — |
| 217 | POST | `performance/goals` | `CreateGoal` | — |
| 221 | PUT | `performance/goals/{id:int}` | `UpdateGoal` | — |
| 225 | DELETE | `performance/goals/{id:int}` | `Ok` | — |
| 229 | GET | `performance/cycles` | `GetReviewCyclesAsync` | — |
| 232 | POST | `performance/cycles` | `CreateCycle` | — |
| 236 | PUT | `performance/cycles/{id:int}` | `UpdateCycle` | — |
| 240 | DELETE | `performance/cycles/{id:int}` | `Ok` | — |
| 244 | GET | `performance/reviews` | `GetReviews` | — |
| 248 | GET | `performance/reviews/{id:int}` | `GetReviewAsync` | — |
| 251 | POST | `performance/reviews` | `CreateReview` | — |
| 255 | PUT | `performance/reviews/{id:int}` | `UpdateReview` | — |
| 259 | DELETE | `performance/reviews/{id:int}` | `Ok` | — |
| 265 | GET | `recruitment/dashboard` | `RecruitmentDashboard` | — |
| 270 | GET | `recruitment/openings` | `GetOpenings` | — |
| 274 | GET | `recruitment/openings/{id:int}` | `GetJobOpeningAsync` | — |
| 277 | POST | `recruitment/openings` | `CreateOpening` | — |
| 281 | PUT | `recruitment/openings/{id:int}` | `UpdateOpening` | — |
| 285 | DELETE | `recruitment/openings/{id:int}` | `Ok` | — |
| 289 | GET | `recruitment/applicants` | `GetApplicants` | — |
| 293 | GET | `recruitment/applicants/{id:int}` | `GetApplicantAsync` | — |
| 296 | POST | `recruitment/applicants` | `CreateApplicant` | — |
| 300 | PUT | `recruitment/applicants/{id:int}` | `UpdateApplicant` | — |
| 304 | POST | `recruitment/applicants/{id:int}/move` | `MoveApplicant` | — |
| 308 | DELETE | `recruitment/applicants/{id:int}` | `Ok` | — |
| 312 | GET | `recruitment/interviews` | `GetInterviews` | — |
| 316 | POST | `recruitment/interviews` | `CreateInterview` | — |
| 320 | PUT | `recruitment/interviews/{id:int}` | `UpdateInterview` | — |
| 324 | DELETE | `recruitment/interviews/{id:int}` | `Ok` | — |
| 328 | GET | `recruitment/applicants/{applicantId:int}/notes` | `GetApplicantNotes` | — |
| 332 | POST | `recruitment/applicants/{applicantId:int}/notes` | `AddApplicantNote` | — |
| 339 | DELETE | `recruitment/notes/{id:int}` | `Ok` | — |

### Incidents

#### `Backend/Modules/Incidents/Controllers/IncidentsController.cs` — `IncidentsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 1

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 34 | POST | `auto` | `ReportAuto` | AllowAnonymous |

### Installations

#### `Backend/Modules/Installations/Controllers/InstallationNotesController.cs` — `InstallationNotesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 5

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 27 | GET | `installation/{installationId}` | `GetNotesByInstallationId` | — |
| 45 | GET | `{id}` | `GetNote` | — |
| 69 | POST | `(vide)` | `CreateNote` | — |
| 99 | PUT | `{id}` | `UpdateNote` | — |
| 128 | DELETE | `{id}` | `DeleteNote` | — |

#### `Backend/Modules/Installations/Controllers/InstallationsController.cs` — `InstallationsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 9

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 46 | GET | `(vide)` | `GetInstallations` | — |
| 76 | GET | `search` | `SearchInstallations` | — |
| 102 | GET | `{id:int}` | `GetInstallationById` | — |
| 126 | POST | `(vide)` | `CreateInstallation` | — |
| 154 | PUT | `{id:int}` | `UpdateInstallation` | — |
| 182 | DELETE | `{id:int}` | `DeleteInstallation` | — |
| 208 | GET | `{id:int}/maintenance-history` | `GetMaintenanceHistory` | — |
| 226 | POST | `{id:int}/maintenance-history` | `AddMaintenanceHistory` | — |
| 258 | POST | `import` | `BulkImportInstallations` | — |

### Invoices

#### `Backend/Modules/Invoices/Controllers/InvoicesController.cs` — `InvoicesController`

- Route de classe : `api/invoices`
- Autorisation de classe : Authorize
- Actions : 11

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 27 | GET | `(vide)` | `List` | — |
| 31 | GET | `{id:int}` | `Get` | — |
| 38 | POST | `(vide)` | `Create` | — |
| 45 | POST | `from-sale/{saleId:int}` | `CreateFromSale` | — |
| 95 | PATCH | `{id:int}` | `Update` | — |
| 99 | POST | `{id:int}/post` | `Post` | — |
| 103 | POST | `{id:int}/void` | `Void` | — |
| 107 | POST | `{id:int}/mark-paid` | `MarkPaid` | — |
| 111 | POST | `{id:int}/reopen` | `Reopen` | — |
| 115 | DELETE | `{id:int}` | `Delete` | — |
| 122 | GET | `{id:int}/activities` | `Activities` | — |

### Lookups

#### `Backend/Modules/Lookups/Controllers/LookupsController.cs` — `LookupsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 120

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 24 | GET | `article-categories` | `GetArticleCategories` | — |
| 39 | GET | `article-categories/{id}` | `GetArticleCategory` | — |
| 56 | POST | `article-categories` | `CreateArticleCategory` | — |
| 71 | PUT | `article-categories/{id}` | `UpdateArticleCategory` | — |
| 88 | DELETE | `article-categories/{id}` | `DeleteArticleCategory` | — |
| 106 | GET | `article-groups` | `GetArticleGroups` | — |
| 121 | GET | `article-groups/{id}` | `GetArticleGroup` | — |
| 138 | POST | `article-groups` | `CreateArticleGroup` | — |
| 153 | PUT | `article-groups/{id}` | `UpdateArticleGroup` | — |
| 170 | DELETE | `article-groups/{id}` | `DeleteArticleGroup` | — |
| 188 | GET | `document-types` | `GetDocumentTypes` | — |
| 203 | GET | `document-types/{id}` | `GetDocumentType` | — |
| 220 | POST | `document-types` | `CreateDocumentType` | — |
| 235 | PUT | `document-types/{id}` | `UpdateDocumentType` | — |
| 252 | DELETE | `document-types/{id}` | `DeleteDocumentType` | — |
| 270 | GET | `article-statuses` | `GetArticleStatuses` | — |
| 285 | POST | `article-statuses` | `CreateArticleStatus` | — |
| 300 | PUT | `article-statuses/{id}` | `UpdateArticleStatus` | — |
| 316 | DELETE | `article-statuses/{id}` | `DeleteArticleStatus` | — |
| 333 | GET | `service-categories` | `GetServiceCategories` | — |
| 348 | POST | `service-categories` | `CreateServiceCategory` | — |
| 363 | PUT | `service-categories/{id}` | `UpdateServiceCategory` | — |
| 379 | DELETE | `service-categories/{id}` | `DeleteServiceCategory` | — |
| 396 | GET | `task-statuses` | `GetTaskStatuses` | — |
| 411 | POST | `task-statuses` | `CreateTaskStatus` | — |
| 426 | PUT | `task-statuses/{id}` | `UpdateTaskStatus` | — |
| 442 | DELETE | `task-statuses/{id}` | `DeleteTaskStatus` | — |
| 459 | GET | `event-types` | `GetEventTypes` | — |
| 474 | POST | `event-types` | `CreateEventType` | — |
| 489 | PUT | `event-types/{id}` | `UpdateEventType` | — |
| 505 | DELETE | `event-types/{id}` | `DeleteEventType` | — |
| 522 | GET | `priorities` | `GetPriorities` | — |
| 537 | POST | `priorities` | `CreatePriority` | — |
| 552 | PUT | `priorities/{id}` | `UpdatePriority` | — |
| 568 | DELETE | `priorities/{id}` | `DeletePriority` | — |
| 585 | GET | `technician-statuses` | `GetTechnicianStatuses` | — |
| 600 | POST | `technician-statuses` | `CreateTechnicianStatus` | — |
| 615 | PUT | `technician-statuses/{id}` | `UpdateTechnicianStatus` | — |
| 631 | DELETE | `technician-statuses/{id}` | `DeleteTechnicianStatus` | — |
| 648 | GET | `leave-types` | `GetLeaveTypes` | — |
| 663 | POST | `leave-types` | `CreateLeaveType` | — |
| 678 | PUT | `leave-types/{id}` | `UpdateLeaveType` | — |
| 694 | DELETE | `leave-types/{id}` | `DeleteLeaveType` | — |
| 711 | GET | `project-statuses` | `GetProjectStatuses` | — |
| 726 | POST | `project-statuses` | `CreateProjectStatus` | — |
| 741 | PUT | `project-statuses/{id}` | `UpdateProjectStatus` | — |
| 757 | DELETE | `project-statuses/{id}` | `DeleteProjectStatus` | — |
| 774 | GET | `project-types` | `GetProjectTypes` | — |
| 789 | POST | `project-types` | `CreateProjectType` | — |
| 804 | PUT | `project-types/{id}` | `UpdateProjectType` | — |
| 820 | DELETE | `project-types/{id}` | `DeleteProjectType` | — |
| 837 | GET | `offer-statuses` | `GetOfferStatuses` | — |
| 852 | POST | `offer-statuses` | `CreateOfferStatus` | — |
| 867 | PUT | `offer-statuses/{id}` | `UpdateOfferStatus` | — |
| 883 | DELETE | `offer-statuses/{id}` | `DeleteOfferStatus` | — |
| 900 | GET | `sale-statuses` | `GetSaleStatuses` | — |
| 915 | POST | `sale-statuses` | `CreateSaleStatus` | — |
| 930 | PUT | `sale-statuses/{id}` | `UpdateSaleStatus` | — |
| 946 | DELETE | `sale-statuses/{id}` | `DeleteSaleStatus` | — |
| 963 | GET | `service-order-statuses` | `GetServiceOrderStatuses` | — |
| 978 | POST | `service-order-statuses` | `CreateServiceOrderStatus` | — |
| 993 | PUT | `service-order-statuses/{id}` | `UpdateServiceOrderStatus` | — |
| 1009 | DELETE | `service-order-statuses/{id}` | `DeleteServiceOrderStatus` | — |
| 1026 | GET | `dispatch-statuses` | `GetDispatchStatuses` | — |
| 1041 | POST | `dispatch-statuses` | `CreateDispatchStatus` | — |
| 1056 | PUT | `dispatch-statuses/{id}` | `UpdateDispatchStatus` | — |
| 1072 | DELETE | `dispatch-statuses/{id}` | `DeleteDispatchStatus` | — |
| 1089 | GET | `offer-categories` | `GetOfferCategories` | — |
| 1104 | GET | `offer-categories/{id}` | `GetOfferCategory` | — |
| 1121 | POST | `offer-categories` | `CreateOfferCategory` | — |
| 1136 | PUT | `offer-categories/{id}` | `UpdateOfferCategory` | — |
| 1152 | DELETE | `offer-categories/{id}` | `DeleteOfferCategory` | — |
| 1169 | GET | `offer-sources` | `GetOfferSources` | — |
| 1184 | GET | `offer-sources/{id}` | `GetOfferSource` | — |
| 1201 | POST | `offer-sources` | `CreateOfferSource` | — |
| 1216 | PUT | `offer-sources/{id}` | `UpdateOfferSource` | — |
| 1232 | DELETE | `offer-sources/{id}` | `DeleteOfferSource` | — |
| 1249 | GET | `skills` | `GetSkills` | — |
| 1264 | POST | `skills` | `CreateSkill` | — |
| 1279 | PUT | `skills/{id}` | `UpdateSkill` | — |
| 1295 | DELETE | `skills/{id}` | `DeleteSkill` | — |
| 1312 | GET | `installation-types` | `GetInstallationTypes` | — |
| 1327 | GET | `installation-types/{id}` | `GetInstallationType` | — |
| 1343 | POST | `installation-types` | `CreateInstallationType` | — |
| 1358 | PUT | `installation-types/{id}` | `UpdateInstallationType` | — |
| 1374 | DELETE | `installation-types/{id}` | `DeleteInstallationType` | — |
| 1391 | GET | `installation-categories` | `GetInstallationCategories` | — |
| 1406 | GET | `installation-categories/{id}` | `GetInstallationCategory` | — |
| 1422 | POST | `installation-categories` | `CreateInstallationCategory` | — |
| 1437 | PUT | `installation-categories/{id}` | `UpdateInstallationCategory` | — |
| 1453 | DELETE | `installation-categories/{id}` | `DeleteInstallationCategory` | — |
| 1470 | GET | `countries` | `GetCountries` | — |
| 1485 | POST | `countries` | `CreateCountry` | — |
| 1500 | PUT | `countries/{id}` | `UpdateCountry` | — |
| 1516 | DELETE | `countries/{id}` | `DeleteCountry` | — |
| 1533 | GET | `locations` | `GetLocations` | — |
| 1548 | GET | `locations/{id}` | `GetLocation` | — |
| 1565 | POST | `locations` | `CreateLocation` | — |
| 1580 | PUT | `locations/{id}` | `UpdateLocation` | — |
| 1597 | DELETE | `locations/{id}` | `DeleteLocation` | — |
| 1615 | GET | `work-types` | `GetWorkTypes` | — |
| 1630 | GET | `work-types/{id}` | `GetWorkType` | — |
| 1647 | POST | `work-types` | `CreateWorkType` | — |
| 1662 | PUT | `work-types/{id}` | `UpdateWorkType` | — |
| 1679 | DELETE | `work-types/{id}` | `DeleteWorkType` | — |
| 1697 | GET | `expense-types` | `GetExpenseTypes` | — |
| 1712 | GET | `expense-types/{id}` | `GetExpenseType` | — |
| 1729 | POST | `expense-types` | `CreateExpenseType` | — |
| 1744 | PUT | `expense-types/{id}` | `UpdateExpenseType` | — |
| 1761 | DELETE | `expense-types/{id}` | `DeleteExpenseType` | — |
| 1779 | GET | `form-categories` | `GetFormCategories` | — |
| 1794 | GET | `form-categories/{id}` | `GetFormCategory` | — |
| 1811 | POST | `form-categories` | `CreateFormCategory` | — |
| 1826 | PUT | `form-categories/{id}` | `UpdateFormCategory` | — |
| 1843 | DELETE | `form-categories/{id}` | `DeleteFormCategory` | — |
| 1861 | GET | `currencies` | `GetCurrencies` | — |
| 1876 | GET | `currencies/{id}` | `GetCurrency` | — |
| 1893 | POST | `currencies` | `CreateCurrency` | — |
| 1908 | PUT | `currencies/{id}` | `UpdateCurrency` | — |
| 1925 | DELETE | `currencies/{id}` | `DeleteCurrency` | — |

#### `Backend/Modules/Lookups/Controllers/PreferencesController.cs` — `PreferencesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 4

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 24 | GET | `{userId}` | `GetUserPreferences` | — |
| 59 | POST | `{userId}` | `CreateUserPreferences` | — |
| 97 | PUT | `{userId}` | `UpdateUserPreferences` | — |
| 132 | DELETE | `{userId}` | `DeleteUserPreferences` | — |

### ModuleRequests

#### `Backend/Modules/ModuleRequests/Controllers/ModuleRequestsController.cs` — `ModuleRequestsController`

- Route de classe : `api/module-requests`
- Autorisation de classe : Authorize
- Actions : 1

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 38 | POST | `(vide)` | `Create` | — |

### Notifications

#### `Backend/Modules/Notifications/Controllers/NotificationsController.cs` — `NotificationsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 8

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 39 | GET | `(vide)` | `GetNotifications` | — |
| 55 | GET | `{id}` | `GetNotification` | — |
| 72 | GET | `unread-count` | `GetUnreadCount` | — |
| 86 | POST | `(vide)` | `CreateNotification` | — |
| 96 | PATCH | `{id}/read` | `MarkAsRead` | — |
| 113 | PATCH | `read` | `MarkMultipleAsRead` | — |
| 127 | PATCH | `read-all` | `MarkAllAsRead` | — |
| 141 | DELETE | `{id}` | `DeleteNotification` | — |

### Numbering

#### `Backend/Modules/Numbering/Controllers/NumberingController.cs` — `NumberingController`

- Route de classe : `api/settings/numbering`
- Autorisation de classe : Authorize
- Actions : 6

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 26 | GET | `(vide)` | `GetAllSettings` | — |
| 49 | GET | `{entity}` | `GetSettings` | — |
| 72 | PUT | `{entity}` | `UpdateSettings` | — |
| 103 | POST | `/api/numbering/preview` | `Preview` | — |
| 140 | GET | `/api/numbering/next` | `GetNext` | — |
| 170 | POST | `validate` | `Validate` | — |

### Offers

#### `Backend/Modules/Offers/Controllers/OffersController.cs` — `OffersController`

- Route de classe : `api/offers`
- Autorisation de classe : Authorize
- Actions : 16

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 43 | GET | `(vide)` | `GetOffers` | — |
| 76 | GET | `stats` | `GetStats` | — |
| 94 | GET | `{id:int}` | `GetOfferById` | — |
| 114 | POST | `(vide)` | `CreateOffer` | — |
| 139 | PATCH | `{id:int}` | `UpdateOffer` | — |
| 167 | DELETE | `{id:int}` | `DeleteOffer` | — |
| 191 | POST | `{id:int}/renew` | `RenewOffer` | — |
| 215 | POST | `{id:int}/send` | `MarkOfferAsSent` | — |
| 239 | POST | `{id:int}/convert` | `ConvertOffer` | — |
| 264 | GET | `{id:int}/activities` | `GetOfferActivities` | — |
| 279 | POST | `{id:int}/activities` | `AddOfferActivity` | — |
| 303 | DELETE | `{id:int}/activities/{activityId:int}` | `DeleteOfferActivity` | — |
| 326 | POST | `{id:int}/items` | `AddOfferItem` | — |
| 349 | PATCH | `{id:int}/items/{itemId:int}` | `UpdateOfferItem` | — |
| 372 | DELETE | `{id:int}/items/{itemId:int}` | `DeleteOfferItem` | — |
| 403 | POST | `import` | `BulkImportOffers` | — |

### OfflineHydration

#### `Backend/Modules/OfflineHydration/Controllers/OfflineHydrationPreferencesController.cs` — `OfflineHydrationPreferencesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 2

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 34 | GET | `(vide)` | `GetMine` | — |
| 56 | PUT | `(vide)` | `PutMine` | — |

### Payments

#### `Backend/Modules/Payments/Controllers/PaymentsController.cs` — `PaymentsController`

- Route de classe : `(aucun [Route] de classe)`
- Autorisation de classe : Authorize
- Actions : 30

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 37 | GET | `api/sales/{entityId}/payments` | `GetSalePayments` | — |
| 52 | POST | `api/sales/{entityId}/payments` | `CreateSalePayment` | — |
| 67 | DELETE | `api/sales/{entityId}/payments/{paymentId}` | `DeleteSalePayment` | — |
| 83 | GET | `api/sales/{entityId}/payments/summary` | `GetSalePaymentSummary` | — |
| 98 | GET | `api/sales/{entityId}/payments/statement` | `GetSalePaymentStatement` | — |
| 113 | GET | `api/sales/{entityId}/payment-plans` | `GetSalePaymentPlans` | — |
| 128 | POST | `api/sales/{entityId}/payment-plans` | `CreateSalePaymentPlan` | — |
| 143 | DELETE | `api/sales/{entityId}/payment-plans/{planId}` | `DeleteSalePaymentPlan` | — |
| 163 | GET | `api/offers/{entityId}/payments` | `GetOfferPayments` | — |
| 178 | POST | `api/offers/{entityId}/payments` | `CreateOfferPayment` | — |
| 193 | DELETE | `api/offers/{entityId}/payments/{paymentId}` | `DeleteOfferPayment` | — |
| 209 | GET | `api/offers/{entityId}/payments/summary` | `GetOfferPaymentSummary` | — |
| 224 | GET | `api/offers/{entityId}/payments/statement` | `GetOfferPaymentStatement` | — |
| 239 | GET | `api/offers/{entityId}/payment-plans` | `GetOfferPaymentPlans` | — |
| 254 | POST | `api/offers/{entityId}/payment-plans` | `CreateOfferPaymentPlan` | — |
| 269 | DELETE | `api/offers/{entityId}/payment-plans/{planId}` | `DeleteOfferPaymentPlan` | — |
| 289 | GET | `api/invoices/{entityId}/payments` | `GetInvoicePayments` | — |
| 304 | POST | `api/invoices/{entityId}/payments` | `CreateInvoicePayment` | — |
| 331 | DELETE | `api/invoices/{entityId}/payments/{paymentId}` | `DeleteInvoicePayment` | — |
| 347 | GET | `api/invoices/{entityId}/payments/summary` | `GetInvoicePaymentSummary` | — |
| 362 | GET | `api/invoices/{entityId}/payments/statement` | `GetInvoicePaymentStatement` | — |
| 377 | GET | `api/invoices/{entityId}/payment-plans` | `GetInvoicePaymentPlans` | — |
| 392 | POST | `api/invoices/{entityId}/payment-plans` | `CreateInvoicePaymentPlan` | — |
| 407 | DELETE | `api/invoices/{entityId}/payment-plans/{planId}` | `DeleteInvoicePaymentPlan` | — |
| 428 | POST | `api/{entityType}/email/send-reminder` | `SendInstallmentReminder` | — |
| 445 | POST | `api/{entityType}/email/send-confirmation` | `SendPaymentConfirmation` | — |
| 476 | GET | `api/{entityType}/{entityId}/payments/{paymentId}/proofs` | `GetPaymentProofs` | — |
| 497 | POST | `api/{entityType}/{entityId}/payments/{paymentId}/proofs` | `AddPaymentProofs` | — |
| 521 | PUT | `api/{entityType}/{entityId}/payments/{paymentId}/proofs/{proofId}` | `UpdatePaymentProof` | — |
| 547 | DELETE | `api/{entityType}/{entityId}/payments/{paymentId}/proofs/{proofId}` | `DeletePaymentProof` | — |

### Planning

#### `Backend/Modules/Planning/Controllers/PlannedLineEntriesController.cs` — `PlannedLineEntriesController`

- Route de classe : `api/planned-entries`
- Autorisation de classe : Authorize
- Actions : 6

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 24 | GET | `(vide)` | `List` | — |
| 28 | POST | `(vide)` | `Create` | — |
| 32 | PUT | `{id:int}` | `Update` | — |
| 36 | DELETE | `{id:int}` | `Delete` | — |
| 46 | GET | `plan-vs-actual/{serviceOrderJobId:int}` | `PlanVsActual` | — |
| 51 | POST | `copy` | `Copy` | — |

#### `Backend/Modules/Planning/Controllers/PlanningController.cs` — `PlanningController`

- Route de classe : `api/planning`
- Autorisation de classe : Authorize
- Actions : 12

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 50 | GET | `unassigned-jobs` | `GetUnassignedJobs` | — |
| 79 | POST | `assign` | `AssignJob` | — |
| 112 | POST | `batch-assign` | `BatchAssign` | — |
| 135 | POST | `validate-assignment` | `ValidateAssignment` | — |
| 153 | GET | `user-schedule/{userId}` | `GetUserSchedule` | — |
| 184 | GET | `available-users` | `GetAvailableUsers` | — |
| 221 | GET | `schedule/{userId}` | `GetUserFullSchedule` | — |
| 243 | PUT | `schedule` | `UpdateUserSchedule` | — |
| 271 | GET | `leaves/{userId}` | `GetUserLeaves` | — |
| 289 | POST | `leaves` | `CreateLeave` | — |
| 316 | PUT | `leaves/{leaveId}` | `UpdateLeave` | — |
| 348 | DELETE | `leaves/{leaveId}` | `DeleteLeave` | — |

### PlanningProfiles

#### `Backend/Modules/PlanningProfiles/Controllers/PlanningProfilesController.cs` — `PlanningProfilesController`

- Route de classe : `api/planning-profiles`
- Autorisation de classe : Authorize
- Actions : 7

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 43 | GET | `(vide)` | `List` | — |
| 50 | GET | `active` | `GetActive` | — |
| 61 | GET | `{id:int}` | `Get` | — |
| 72 | POST | `(vide)` | `Create` | — |
| 88 | PUT | `{id:int}` | `Update` | — |
| 104 | DELETE | `{id:int}` | `Delete` | — |
| 119 | PUT | `active/{id:int}` | `SetActive` | — |

### Plugins

#### `Backend/Modules/Plugins/Controllers/PluginsController.cs` — `PluginsController`

- Route de classe : `api/plugins`
- Autorisation de classe : Authorize
- Actions : 5

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 29 | GET | `(vide)` | `List` | — |
| 33 | GET | `stats` | `Stats` | — |
| 37 | PATCH | `{code}` | `Toggle` | — |
| 66 | POST | `bulk` | `Bulk` | — |
| 78 | GET | `graph` | `Ok` | AllowAnonymous |

#### `Backend/Modules/Plugins/Controllers/PublicPluginsController.cs` — `PublicPluginsController`

- Route de classe : `api/public/plugins`
- Autorisation de classe : AllowAnonymous
- Actions : 8

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 106 | GET | `tenants` | `Tenants` | — |
| 111 | GET | `graph` | `Ok` | — |
| 128 | GET | `(vide)` | `Get` | — |
| 137 | GET | `all` | `All` | — |
| 161 | GET | `preview/{code}` | `Preview` | — |
| 191 | PATCH | `{code}` | `Toggle` | — |
| 224 | POST | `bulk` | `Bulk` | — |
| 244 | POST | `broadcast` | `Broadcast` | — |

### Preferences

#### `Backend/Modules/Preferences/Controllers/PdfSettingsController.cs` — `PdfSettingsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 5

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 28 | GET | `(vide)` | `GetAllSettings` | — |
| 55 | GET | `{module}` | `GetSettingsByModule` | — |
| 108 | PUT | `{module}` | `UpdateSettings` | — |
| 145 | POST | `(vide)` | `CreateSettings` | — |
| 182 | DELETE | `{module}` | `DeleteSettings` | — |

#### `Backend/Modules/Preferences/Controllers/PreferencesController.cs` — `PreferencesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 7

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 32 | GET | `(vide)` | `GetMyPreferences` | — |
| 57 | GET | `{userId}` | `GetUserPreferences` | — |
| 77 | POST | `(vide)` | `CreateMyPreferences` | — |
| 96 | POST | `{userId}` | `CreateUserPreferences` | — |
| 110 | PUT | `(vide)` | `UpdateMyPreferences` | — |
| 135 | PUT | `{userId}` | `UpdateUserPreferences` | — |
| 155 | DELETE | `(vide)` | `DeleteMyPreferences` | — |

### Processes

#### `Backend/Modules/Processes/Controllers/ProcessesController.cs` — `ProcessesController`

- Route de classe : `api/processes`
- Autorisation de classe : Authorize
- Actions : 10

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 80 | GET | `schemas` | `Schemas` | — |
| 91 | GET | `schedules` | `List` | — |
| 203 | PUT | `schedules` | `Upsert` | — |
| 264 | POST | `schedules/{key}/pause` | `SetPaused` | — |
| 288 | POST | `schedules/{key}/enable` | `SetEnabled` | — |
| 306 | POST | `schedules/{key}/reset-failures` | `ResetFailures` | — |
| 327 | GET | `runs/{key}` | `ListRuns` | — |
| 347 | GET | `running-keys` | `RunningKeys` | — |
| 363 | POST | `run` | `RunNow` | — |
| 427 | POST | `schedules/{key}/stop` | `StopRun` | — |

### Projects

#### `Backend/Modules/Projects/Controllers/ProjectColumnsController.cs` — `ProjectColumnsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 6

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 27 | GET | `project/{projectId}` | `GetProjectColumns` | — |
| 45 | GET | `{id}` | `GetColumn` | — |
| 69 | POST | `(vide)` | `CreateColumn` | — |
| 94 | PUT | `{id}` | `UpdateColumn` | — |
| 124 | DELETE | `{id}` | `DeleteColumn` | — |
| 149 | PUT | `project/{projectId}/reorder` | `ReorderColumns` | — |

#### `Backend/Modules/Projects/Controllers/ProjectsController.cs` — `ProjectsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 21

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 33 | GET | `(vide)` | `GetAllProjects` | — |
| 52 | GET | `{id}` | `GetProject` | — |
| 77 | POST | `(vide)` | `CreateProject` | — |
| 111 | PUT | `{id}` | `UpdateProject` | — |
| 150 | DELETE | `{id}` | `DeleteProject` | — |
| 178 | GET | `search` | `SearchProjects` | — |
| 200 | GET | `statistics` | `GetStatistics` | — |
| 218 | POST | `bulk/status` | `BulkUpdateStatus` | — |
| 243 | POST | `bulk/archive` | `BulkArchive` | — |
| 264 | GET | `{projectId}/notes` | `GetProjectNotes` | — |
| 282 | POST | `{projectId}/notes` | `CreateProjectNote` | — |
| 313 | DELETE | `notes/{noteId}` | `DeleteProjectNote` | — |
| 342 | GET | `{projectId}/activity` | `GetProjectActivity` | — |
| 357 | GET | `{projectId}/links` | `GetProjectLinks` | — |
| 377 | POST | `{projectId}/links` | `LinkEntityToProject` | — |
| 405 | DELETE | `{projectId}/links/{entityType}/{entityId:int}` | `UnlinkEntityFromProject` | — |
| 432 | GET | `settings` | `GetProjectSettings` | — |
| 447 | PUT | `settings` | `UpdateProjectSettings` | — |
| 462 | GET | `{projectId}/team-members` | `GetTeamMembers` | — |
| 470 | POST | `{projectId}/team-members` | `AssignTeamMember` | — |
| 485 | DELETE | `{projectId}/team-members` | `RemoveTeamMember` | — |

#### `Backend/Modules/Projects/Controllers/RecurringTasksController.cs` — `RecurringTasksController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 12

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 30 | POST | `(vide)` | `Create` | — |
| 42 | GET | `{id}` | `GetById` | — |
| 50 | PUT | `{id}` | `Update` | — |
| 58 | DELETE | `{id}` | `Delete` | — |
| 67 | GET | `project-task/{projectTaskId}` | `GetForProjectTask` | — |
| 74 | GET | `daily-task/{dailyTaskId}` | `GetForDailyTask` | — |
| 81 | GET | `active` | `GetAllActive` | — |
| 88 | GET | `{id}/logs` | `GetLogs` | — |
| 96 | POST | `{id}/pause` | `Pause` | — |
| 104 | POST | `{id}/resume` | `Resume` | — |
| 113 | POST | `generate` | `GenerateDueTasks` | — |
| 120 | GET | `{id}/next-occurrence` | `GetNextOccurrence` | — |

#### `Backend/Modules/Projects/Controllers/TaskAttachmentsController.cs` — `TaskAttachmentsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 10

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 27 | GET | `project-task/{projectTaskId}` | `GetProjectTaskAttachments` | — |
| 45 | GET | `daily-task/{dailyTaskId}` | `GetDailyTaskAttachments` | — |
| 63 | GET | `{id}` | `GetAttachment` | — |
| 87 | POST | `(vide)` | `CreateAttachment` | — |
| 117 | PUT | `{id}` | `UpdateAttachment` | — |
| 152 | DELETE | `{id}` | `DeleteAttachment` | — |
| 182 | GET | `search` | `SearchAttachments` | — |
| 200 | GET | `images` | `GetImageAttachments` | — |
| 218 | GET | `documents` | `GetDocumentAttachments` | — |
| 236 | DELETE | `bulk` | `BulkDeleteAttachments` | — |

#### `Backend/Modules/Projects/Controllers/TaskChecklistsController.cs` — `TaskChecklistsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 13

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 36 | POST | `(vide)` | `CreateChecklist` | — |
| 48 | GET | `{id}` | `GetChecklist` | — |
| 56 | PUT | `{id}` | `UpdateChecklist` | — |
| 64 | DELETE | `{id}` | `DeleteChecklist` | — |
| 73 | GET | `project-task/{projectTaskId}` | `GetChecklistsForProjectTask` | — |
| 80 | GET | `daily-task/{dailyTaskId}` | `GetChecklistsForDailyTask` | — |
| 88 | POST | `items` | `CreateChecklistItem` | — |
| 95 | PUT | `items/{id}` | `UpdateChecklistItem` | — |
| 103 | DELETE | `items/{id}` | `DeleteChecklistItem` | — |
| 111 | POST | `items/{id}/toggle` | `ToggleChecklistItem` | — |
| 122 | POST | `items/bulk` | `BulkCreateChecklistItems` | — |
| 129 | POST | `items/reorder` | `ReorderChecklistItems` | — |
| 137 | POST | `items/{id}/convert-to-task` | `ConvertItemToTask` | — |

#### `Backend/Modules/Projects/Controllers/TaskCommentsController.cs` — `TaskCommentsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 6

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 27 | GET | `task/{taskId}` | `GetTaskComments` | — |
| 45 | GET | `{id}` | `GetComment` | — |
| 69 | POST | `(vide)` | `CreateComment` | — |
| 99 | PUT | `{id}` | `UpdateComment` | — |
| 134 | DELETE | `{id}` | `DeleteComment` | — |
| 164 | GET | `search` | `SearchComments` | — |

#### `Backend/Modules/Projects/Controllers/TaskTimeEntriesController.cs` — `TaskTimeEntriesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 18

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 36 | POST | `(vide)` | `CreateTimeEntry` | — |
| 54 | GET | `{id}` | `GetTimeEntry` | — |
| 65 | PUT | `{id}` | `UpdateTimeEntry` | — |
| 76 | DELETE | `{id}` | `DeleteTimeEntry` | — |
| 88 | GET | `project-task/{projectTaskId}` | `GetTimeEntriesForProjectTask` | — |
| 95 | GET | `daily-task/{dailyTaskId}` | `GetTimeEntriesForDailyTask` | — |
| 102 | GET | `user/{userId}` | `GetTimeEntriesByUser` | — |
| 112 | GET | `project/{projectId}` | `GetTimeEntriesByProject` | — |
| 122 | POST | `query` | `QueryTimeEntries` | — |
| 130 | GET | `summary/project-task/{projectTaskId}` | `GetProjectTaskTimeSummary` | — |
| 137 | GET | `summary/daily-task/{dailyTaskId}` | `GetDailyTaskTimeSummary` | — |
| 144 | GET | `total-time` | `GetTotalLoggedTime` | — |
| 154 | POST | `{id}/approve` | `ApproveTimeEntry` | — |
| 168 | POST | `bulk-approve` | `BulkApproveTimeEntries` | — |
| 181 | POST | `timer/start` | `StartTimer` | — |
| 197 | POST | `timer/{id}/stop` | `StopTimer` | — |
| 210 | GET | `timer/active` | `GetActiveTimer` | — |
| 228 | GET | `my-entries` | `GetMyTimeEntries` | — |

#### `Backend/Modules/Projects/Controllers/TasksController.cs` — `TasksController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 25

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 29 | GET | `project/{projectId}` | `GetProjectTasks` | — |
| 47 | GET | `entity/{entityType}/{entityId}` | `GetEntityTasks` | — |
| 65 | GET | `project-task/{id}` | `GetProjectTask` | — |
| 89 | POST | `project-task` | `CreateProjectTask` | — |
| 119 | PUT | `project-task/{id}` | `UpdateProjectTask` | — |
| 154 | DELETE | `project-task/{id}` | `DeleteProjectTask` | — |
| 183 | GET | `daily/user/{userId}` | `GetUserDailyTasks` | — |
| 201 | GET | `daily-task/{id}` | `GetDailyTask` | — |
| 225 | POST | `daily-task` | `CreateDailyTask` | — |
| 255 | PUT | `daily-task/{id}` | `UpdateDailyTask` | — |
| 290 | DELETE | `daily-task/{id}` | `DeleteDailyTask` | — |
| 319 | GET | `search` | `SearchTasks` | — |
| 337 | GET | `assignee/{assigneeId}` | `GetTasksByAssignee` | — |
| 355 | GET | `overdue` | `GetOverdueTasks` | — |
| 379 | PUT | `{taskId}/move` | `MoveTask` | — |
| 409 | PUT | `bulk/move` | `BulkMoveTasks` | — |
| 443 | PUT | `{taskId}/assign` | `AssignTask` | — |
| 473 | PUT | `{taskId}/unassign` | `UnassignTask` | — |
| 498 | PUT | `bulk/assign` | `BulkAssignTasks` | — |
| 534 | PUT | `bulk/status` | `BulkUpdateTaskStatus` | — |
| 569 | GET | `project/{projectId}/status-counts` | `GetProjectStatusCounts` | — |
| 587 | GET | `project/{projectId}/completion-percentage` | `GetProjectCompletionPercentage` | — |
| 607 | GET | `user/{userId}/status-counts` | `GetUserStatusCounts` | — |
| 628 | GET | `user/{userId}/overdue-count` | `GetUserOverdueCount` | — |
| 647 | GET | `projects/bulk-stats` | `GetBulkProjectStats` | — |

### Purchases

#### `Backend/Modules/Purchases/Controllers/ArticleSuppliersController.cs` — `ArticleSuppliersController`

- Route de classe : `api/article-suppliers`
- Autorisation de classe : Authorize
- Actions : 7

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 32 | GET | `by-article/{articleId:int}` | `GetByArticle` | RequirePermission(purchases, read) |
| 50 | GET | `by-supplier/{supplierId:int}` | `GetBySupplier` | RequirePermission(purchases, read) |
| 68 | GET | `{id:int}` | `GetById` | RequirePermission(purchases, read) |
| 87 | POST | `(vide)` | `Create` | RequirePermission(purchases, create) |
| 107 | PATCH | `{id:int}` | `Update` | RequirePermission(purchases, update) |
| 127 | DELETE | `{id:int}` | `Delete` | RequirePermission(purchases, delete) |
| 147 | GET | `{id:int}/price-history` | `GetPriceHistory` | RequirePermission(purchases, read) |

#### `Backend/Modules/Purchases/Controllers/GoodsReceiptsController.cs` — `GoodsReceiptsController`

- Route de classe : `api/goods-receipts`
- Autorisation de classe : Authorize
- Actions : 6

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 32 | GET | `(vide)` | `GetReceipts` | RequirePermission(purchases, read) |
| 53 | GET | `{id:int}/activities` | `GetActivities` | RequirePermission(purchases, read) |
| 69 | GET | `{id:int}` | `GetReceipt` | RequirePermission(purchases, read) |
| 86 | POST | `(vide)` | `CreateReceipt` | RequirePermission(purchases, create) |
| 126 | PATCH / PUT | `{id:int} / {id:int}` | `UpdateReceipt` | RequirePermission(purchases, update) |
| 154 | DELETE | `{id:int}` | `DeleteReceipt` | RequirePermission(purchases, delete) |

#### `Backend/Modules/Purchases/Controllers/PurchaseActivitiesController.cs` — `PurchaseActivitiesController`

- Route de classe : `api/purchase-activities`
- Autorisation de classe : Authorize
- Actions : 1

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 30 | GET | `(vide)` | `GetActivities` | RequirePermission(audit_logs, read) |

#### `Backend/Modules/Purchases/Controllers/PurchaseOrdersController.cs` — `PurchaseOrdersController`

- Route de classe : `api/purchase-orders`
- Autorisation de classe : Authorize
- Actions : 12

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 40 | GET | `{id:int}/tej-xml` | `DownloadTejXml` | RequirePermission(purchases, read) |
| 70 | GET | `(vide)` | `GetOrders` | RequirePermission(purchases, read) |
| 91 | GET | `stats` | `GetStats` | RequirePermission(purchases, read) |
| 107 | GET | `{id:int}` | `GetOrder` | RequirePermission(purchases, read) |
| 129 | GET | `new` | `GetNewSentinel` | RequirePermission(purchases, read) |
| 144 | POST | `(vide)` | `CreateOrder` | RequirePermission(purchases, create) |
| 177 | PATCH | `{id:int}` | `UpdateOrder` | RequirePermission(purchases, update) |
| 199 | DELETE | `{id:int}` | `DeleteOrder` | RequirePermission(purchases, delete) |
| 219 | GET | `{id:int}/activities` | `GetActivities` | RequirePermission(purchases, read) |
| 235 | POST | `{id:int}/items` | `AddItem` | RequirePermission(purchases, create) |
| 254 | PATCH | `{id:int}/items/{itemId:int}` | `UpdateItem` | RequirePermission(purchases, update) |
| 273 | DELETE | `{id:int}/items/{itemId:int}` | `DeleteItem` | RequirePermission(purchases, delete) |

#### `Backend/Modules/Purchases/Controllers/SupplierInvoicesController.cs` — `SupplierInvoicesController`

- Route de classe : `api/supplier-invoices`
- Autorisation de classe : Authorize
- Actions : 11

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 36 | GET | `{id:int}/tej-xml` | `DownloadTejXml` | RequirePermission(purchases, read) |
| 73 | GET | `(vide)` | `GetInvoices` | RequirePermission(purchases, read) |
| 95 | GET | `{id:int}` | `GetInvoice` | RequirePermission(purchases, read) |
| 112 | POST | `(vide)` | `CreateInvoice` | RequirePermission(purchases, create) |
| 150 | PATCH | `{id:int}` | `UpdateInvoice` | RequirePermission(purchases, update) |
| 181 | DELETE | `{id:int}` | `DeleteInvoice` | RequirePermission(purchases, delete) |
| 218 | POST | `{id:int}/facture-en-ligne` | `RecordFactureEnLigneSubmission` | RequirePermission(purchases, update) |
| 262 | GET | `{id:int}/activities` | `GetActivities` | RequirePermission(purchases, read) |
| 279 | POST | `{id:int}/items` | `AddItem` | RequirePermission(purchases, create) |
| 297 | PATCH | `{id:int}/items/{itemId:int}` | `UpdateItem` | RequirePermission(purchases, update) |
| 315 | DELETE | `{id:int}/items/{itemId:int}` | `DeleteItem` | RequirePermission(purchases, delete) |

### Reporting

#### `Backend/Modules/Reporting/Controllers/ReportingFavoritesController.cs` — `ReportingFavoritesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 5

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 45 | GET | `(vide)` | `Get` | — |
| 64 | POST | `(vide)` | `Upsert` | — |
| 92 | DELETE | `{widgetId}` | `Delete` | — |
| 111 | DELETE | `(vide)` | `DeleteAll` | — |
| 130 | PUT | `reorder` | `Reorder` | — |

### RetenueSource

#### `Backend/Modules/RetenueSource/Controllers/RSController.cs` — `RSController`

- Route de classe : `api/retenue-source`
- Autorisation de classe : Authorize
- Actions : 9

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 34 | GET | `(vide)` | `GetRSRecords` | RequirePermission(purchases, read) |
| 64 | GET | `{id:int}` | `GetRSRecordById` | RequirePermission(purchases, read) |
| 85 | POST | `(vide)` | `CreateRSRecord` | RequirePermission(purchases, create) |
| 113 | PATCH | `{id:int}` | `UpdateRSRecord` | RequirePermission(purchases, update) |
| 140 | DELETE | `{id:int}` | `DeleteRSRecord` | RequirePermission(purchases, delete) |
| 167 | GET | `calculate` | `CalculateRS` | RequirePermission(purchases, read) |
| 187 | POST | `tej-export` | `ExportTEJ` | RequirePermission(purchases, update) |
| 210 | GET | `tej-logs` | `GetTEJExportLogs` | RequirePermission(purchases, read) |
| 231 | GET | `stats` | `GetRSStats` | RequirePermission(purchases, read) |

### Roles

#### `Backend/Modules/Roles/Controllers/PermissionsController.cs` — `PermissionsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 10

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 32 | GET | `role/{roleId}` | `GetRolePermissions` | — |
| 54 | PUT | `role/{roleId}` | `UpdateRolePermissions` | — |
| 79 | POST | `role/{roleId}/set` | `SetPermission` | — |
| 109 | DELETE | `role/{roleId}/{module}/{action}` | `DeletePermission` | — |
| 131 | GET | `user/{userId}` | `GetUserPermissions` | — |
| 149 | POST | `user/{userId}/check` | `CheckUserPermission` | — |
| 180 | POST | `role/{roleId}/grant-all` | `GrantAllPermissions` | — |
| 203 | POST | `role/{roleId}/revoke-all` | `RevokeAllPermissions` | — |
| 222 | POST | `role/{roleId}/module/{module}/grant` | `GrantModulePermissions` | — |
| 245 | POST | `role/{roleId}/module/{module}/revoke` | `RevokeModulePermissions` | — |

#### `Backend/Modules/Roles/Controllers/RolesController.cs` — `RolesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 9

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 29 | GET | `(vide)` | `GetAllRoles` | — |
| 50 | GET | `all-user-roles` | `GetAllUserRoles` | — |
| 65 | GET | `{id}` | `GetRole` | — |
| 85 | POST | `(vide)` | `CreateRole` | — |
| 113 | PUT | `{id}` | `UpdateRole` | — |
| 144 | DELETE | `{id}` | `DeleteRole` | — |
| 164 | POST | `{roleId}/assign/{userId}` | `AssignRoleToUser` | — |
| 181 | DELETE | `{roleId}/remove/{userId}` | `RemoveRoleFromUser` | — |
| 201 | GET | `user/{userId}` | `GetUserRoles` | — |

### Sales

#### `Backend/Modules/Sales/Controllers/SalesController.cs` — `SalesController`

- Route de classe : `api/sales`
- Autorisation de classe : Authorize
- Actions : 12

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 48 | GET | `(vide)` | `GetSales` | — |
| 80 | GET | `stats` | `GetStats` | — |
| 98 | GET | `{id:int}` | `GetSaleById` | — |
| 118 | POST | `(vide)` | `CreateSale` | — |
| 143 | POST | `from-offer/{offerId:int}` | `CreateSaleFromOffer` | — |
| 168 | PATCH | `{id:int}` | `UpdateSale` | — |
| 196 | DELETE | `{id:int}` | `DeleteSale` | — |
| 220 | GET | `{id:int}/activities` | `GetSaleActivities` | — |
| 235 | POST | `{id:int}/activities` | `AddSaleActivity` | — |
| 286 | POST | `{id:int}/items` | `AddSaleItem` | — |
| 309 | PATCH | `{id:int}/items/{itemId:int}` | `UpdateSaleItem` | — |
| 332 | DELETE | `{id:int}/items/{itemId:int}` | `DeleteSaleItem` | — |

### ServiceOrders

#### `Backend/Modules/ServiceOrders/Controllers/ServiceOrdersController.cs` — `ServiceOrdersController`

- Route de classe : `api/service-orders`
- Autorisation de classe : Authorize
- Actions : 33

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 48 | POST | `from-sale/{saleId:int}` | `CreateFromSale` | — |
| 86 | POST | `direct` | `CreateDirect` | — |
| 128 | GET | `(vide)` | `GetServiceOrders` | — |
| 160 | GET | `{id:int}` | `GetServiceOrderById` | — |
| 179 | GET | `{serviceOrderId:int}/jobs/{jobId:int}` | `GetServiceOrderJob` | — |
| 197 | POST | `{serviceOrderId:int}/jobs` | `CreateServiceOrderJob` | — |
| 240 | PATCH | `{serviceOrderId:int}/jobs/{jobId:int}/status` | `PatchServiceOrderJobStatus` | — |
| 263 | PUT | `{serviceOrderId:int}/jobs/{jobId:int}` | `UpdateServiceOrderJob` | — |
| 285 | PUT | `{id:int}` | `UpdateServiceOrder` | — |
| 309 | PATCH | `{id:int}` | `PatchServiceOrder` | — |
| 333 | PUT | `{id:int}/status` | `UpdateStatus` | — |
| 363 | POST | `{id:int}/recalculate-status` | `RecalculateStatus` | — |
| 383 | POST | `{id:int}/approve` | `Approve` | — |
| 411 | POST | `{id:int}/complete` | `Complete` | — |
| 443 | POST | `{id:int}/retry-shadow-sale` | `RetryShadowSale` | — |
| 469 | POST | `{id:int}/cancel` | `Cancel` | — |
| 493 | DELETE | `{id:int}` | `Delete` | — |
| 519 | GET | `statistics` | `GetStatistics` | — |
| 541 | GET | `{id:int}/dispatches` | `GetDispatches` | — |
| 560 | GET | `{id:int}/time-entries` | `GetTimeEntries` | — |
| 579 | GET | `{id:int}/expenses` | `GetExpenses` | — |
| 598 | GET | `{id:int}/materials` | `GetMaterials` | — |
| 617 | POST | `{id:int}/materials` | `AddMaterial` | — |
| 641 | PUT | `{id:int}/materials/{materialId:int}` | `UpdateMaterial` | — |
| 663 | DELETE | `{id:int}/materials/{materialId:int}` | `DeleteMaterial` | — |
| 685 | GET | `{id:int}/notes` | `GetNotes` | — |
| 704 | POST | `{id:int}/notes` | `AddNote` | — |
| 759 | GET | `{id:int}/full-summary` | `GetFullSummary` | — |
| 780 | POST | `{id:int}/time-entries` | `AddTimeEntry` | — |
| 804 | DELETE | `{id:int}/time-entries/{timeEntryId:int}` | `DeleteTimeEntry` | — |
| 828 | POST | `{id:int}/expenses` | `AddExpense` | — |
| 852 | DELETE | `{id:int}/expenses/{expenseId:int}` | `DeleteExpense` | — |
| 876 | POST | `{id:int}/prepare-invoice` | `PrepareForInvoice` | — |

### Settings

#### `Backend/Modules/Settings/Controllers/AppSettingsController.cs` — `AppSettingsController`

- Route de classe : `api/settings/app`
- Autorisation de classe : Authorize
- Actions : 3

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 26 | GET | `(vide)` | `GetAll` | — |
| 36 | GET | `{key}` | `GetByKey` | — |
| 49 | PUT | `{key}` | `Update` | — |

#### `Backend/Modules/Settings/Controllers/ModuleScopeController.cs` — `ModuleScopeController`

- Route de classe : `api/module-scope`
- Autorisation de classe : Authorize
- Actions : 4

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 54 | GET | `(vide)` | `List` | — |
| 70 | GET | `{moduleKey}` | `Get` | — |
| 97 | PUT | `{moduleKey}` | `Update` | — |
| 147 | PUT | `(vide)` | `BulkUpdate` | — |

### Shared

#### `Backend/Modules/Shared/Controllers/DevController.cs` — `DevController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 3

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 28 | GET | `token` | `GenerateDevToken` | — |
| 65 | GET | `permanent-token` | `GeneratePermanentToken` | — |
| 103 | GET | `info` | `GetApiInfo` | — |

#### `Backend/Modules/Shared/Controllers/EntityFormDocumentsController.cs` — `EntityFormDocumentsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 6

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 35 | GET | `{entityType}/{entityId}` | `GetByEntity` | — |
| 55 | GET | `{id:int}` | `GetById` | — |
| 77 | POST | `(vide)` | `Create` | — |
| 109 | PUT | `{id}` | `Update` | — |
| 137 | DELETE | `{id}` | `Delete` | — |
| 161 | POST | `copy` | `CopyDocuments` | — |

#### `Backend/Modules/Shared/Controllers/LogsController.cs` — `LogsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 3

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 25 | GET | `(vide)` | `GetLogs` | — |
| 43 | DELETE | `(vide)` | `ClearLogs` | — |
| 54 | POST | `test` | `TestLog` | — |

#### `Backend/Modules/Shared/Controllers/SystemLogsController.cs` — `SystemLogsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 7

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 25 | GET | `(vide)` | `GetLogs` | — |
| 65 | GET | `{id}` | `GetLogById` | — |
| 87 | POST | `(vide)` | `CreateLog` | — |
| 113 | GET | `statistics` | `GetStatistics` | — |
| 131 | GET | `modules` | `GetModules` | — |
| 149 | DELETE | `cleanup` | `CleanupOldLogs` | — |
| 180 | GET | `export` | `ExportLogs` | — |

#### `Backend/Modules/Shared/Controllers/UploadController.cs` — `UploadController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 3

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 25 | POST | `file` | `UploadFile` | — |
| 71 | POST | `files` | `UploadFiles` | — |
| 122 | DELETE | `{fileKey}` | `DeleteFile` | — |

#### `Backend/Modules/Shared/Controllers/UploadThingController.cs` — `UploadThingController`

- Route de classe : `api/uploadthing`
- Autorisation de classe : Authorize
- Actions : 2

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 76 | POST | `prepare` | `PrepareUpload` | — |
| 149 | POST | `delete` | `DeleteFiles` | — |

### Signatures

#### `Backend/Modules/Signatures/Controllers/SignaturesController.cs` — `SignaturesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 3

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 35 | GET | `me` | `GetMySignature` | — |
| 63 | PUT | `me` | `SaveMySignature` | — |
| 91 | DELETE | `me` | `DeleteMySignature` | — |

### Skills

#### `Backend/Modules/Skills/Controllers/SkillsController.cs` — `SkillsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 12

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 23 | GET | `(vide)` | `GetAllSkills` | — |
| 38 | GET | `{id}` | `GetSkill` | — |
| 58 | POST | `(vide)` | `CreateSkill` | — |
| 91 | PUT | `{id}` | `UpdateSkill` | — |
| 127 | DELETE | `{id}` | `DeleteSkill` | — |
| 147 | GET | `category/{category}` | `GetSkillsByCategory` | — |
| 162 | POST | `{skillId}/assign/{userId}` | `AssignSkillToUser` | — |
| 179 | DELETE | `{skillId}/remove/{userId}` | `RemoveSkillFromUser` | — |
| 199 | GET | `user/{userId}` | `GetUserSkills` | — |
| 214 | POST | `role/{roleId}/assign/{skillId}` | `AssignSkillToRole` | — |
| 235 | DELETE | `role/{roleId}/remove/{skillId}` | `RemoveSkillFromRole` | — |
| 255 | GET | `role/{roleId}` | `GetRoleSkills` | — |

### SupportTickets

#### `Backend/Modules/SupportTickets/Controllers/PublicTicketsController.cs` — `PublicTicketsController`

- Route de classe : `api/public`
- Autorisation de classe : AllowAnonymous
- Actions : 6

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 44 | GET | `tenants` | `ListTenants` | — |
| 57 | GET | `tickets` | `ListTickets` | — |
| 156 | GET | `tickets/{tenant}/{id:int}` | `GetTicket` | — |
| 170 | PATCH | `tickets/{tenant}/{id:int}/status` | `UpdateStatus` | — |
| 192 | GET | `tickets/{tenant}/{id:int}/comments` | `GetComments` | — |
| 220 | POST | `tickets/{tenant}/{id:int}/comments` | `AddComment` | — |

#### `Backend/Modules/SupportTickets/Controllers/SupportTicketsController.cs` — `SupportTicketsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : —
- Actions : 10

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 34 | POST | `(vide)` | `Create` | AllowAnonymous |
| 108 | GET | `(vide)` | `GetAll` | Authorize |
| 155 | GET | `{id:int}` | `GetById` | Authorize |
| 171 | PATCH | `{id:int}/status` | `UpdateStatus` | Authorize |
| 232 | GET | `{id:int}/comments` | `GetComments` | Authorize |
| 261 | POST | `{id:int}/comments` | `AddComment` | Authorize |
| 349 | GET | `{id:int}/links` | `GetLinks` | Authorize |
| 382 | POST | `{id:int}/links` | `AddLink` | Authorize |
| 425 | DELETE | `{id:int}/links/{linkId:int}` | `RemoveLink` | Authorize |
| 443 | GET | `search` | `Search` | Authorize |

### Sync

#### `Backend/Modules/Sync/Controllers/SyncController.cs` — `SyncController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 4

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 27 | POST | `push` | `Push` | — |
| 75 | GET | `pull` | `Pull` | — |
| 91 | GET | `history` | `History` | — |
| 100 | POST | `retry` | `Retry` | — |

### Tenants

#### `Backend/Modules/Tenants/Controllers/TenantsController.cs` — `TenantsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 7

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 85 | GET | `(vide)` | `GetAll` | — |
| 146 | GET | `{id}` | `GetById` | — |
| 162 | POST | `(vide)` | `Create` | — |
| 235 | PUT | `{id}` | `Update` | — |
| 281 | DELETE | `{id}` | `Delete` | — |
| 308 | POST | `{id}/set-default` | `SetDefault` | — |
| 347 | POST | `{id}/logo` | `UploadLogo` | — |

### UserAiSettings

#### `Backend/Modules/UserAiSettings/Controllers/UserAiSettingsController.cs` — `UserAiSettingsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 7

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 53 | GET | `keys` | `GetKeys` | — |
| 78 | POST | `keys` | `AddKey` | — |
| 106 | PUT | `keys/{id}` | `UpdateKey` | — |
| 135 | DELETE | `keys/{id}` | `DeleteKey` | — |
| 164 | POST | `keys/reorder` | `ReorderKeys` | — |
| 197 | GET | `preferences` | `GetPreferences` | — |
| 222 | PUT | `preferences` | `UpdatePreferences` | — |

### UserGroups

#### `Backend/Modules/UserGroups/Controllers/UserGroupsController.cs` — `UserGroupsController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 9

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 29 | GET | `(vide)` | `GetAll` | — |
| 45 | GET | `user/{userId}` | `GetUserGroups` | — |
| 60 | GET | `{id}` | `GetById` | — |
| 76 | POST | `(vide)` | `Create` | — |
| 97 | PUT | `{id}` | `Update` | — |
| 121 | DELETE | `{id}` | `Delete` | — |
| 137 | GET | `{id}/members` | `GetMembers` | — |
| 152 | POST | `{id}/members` | `AssignMembers` | — |
| 167 | DELETE | `{groupId}/members/{userId}` | `RemoveMember` | — |

### Users

#### `Backend/Modules/Users/Controllers/UsersController.cs` — `UsersController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 12

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 26 | GET | `(vide)` | `GetAllUsers` | — |
| 50 | GET | `{id}` | `GetUser` | — |
| 83 | POST | `(vide)` | `CreateUser` | — |
| 148 | PUT | `{id}` | `UpdateUser` | — |
| 217 | PUT | `{id}/profile-picture` | `UpdateUserProfilePicture` | — |
| 248 | DELETE | `{id}` | `DeleteUser` | — |
| 291 | POST | `{id}/change-password` | `ChangeUserPassword` | — |
| 348 | GET | `email/{email}` | `GetUserByEmail` | — |
| 381 | GET | `check-email` | `CheckEmailExists` | — |
| 419 | POST | `forgot-password` | `ForgotPassword` | AllowAnonymous |
| 459 | POST | `verify-otp` | `VerifyOtp` | AllowAnonymous |
| 502 | POST | `reset-password` | `ResetPassword` | AllowAnonymous |

### WebsiteBuilder

#### `Backend/Modules/WebsiteBuilder/Controllers/WBPagesController.cs` — `WBPagesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 10

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 24 | GET | `site/{siteId}` | `GetPagesBySiteId` | — |
| 39 | GET | `{id}` | `GetPage` | — |
| 55 | POST | `(vide)` | `CreatePage` | — |
| 72 | PUT | `{id}` | `UpdatePage` | — |
| 94 | PUT | `{id}/components` | `UpdatePageComponents` | — |
| 121 | DELETE | `{id}` | `DeletePage` | — |
| 137 | PUT | `reorder` | `ReorderPages` | — |
| 156 | GET | `{id}/versions` | `GetPageVersions` | — |
| 171 | POST | `{id}/versions` | `SavePageVersion` | — |
| 190 | POST | `{id}/versions/{versionId}/restore` | `RestorePageVersion` | — |

#### `Backend/Modules/WebsiteBuilder/Controllers/WBSitesController.cs` — `WBSitesController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 9

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 24 | GET | `(vide)` | `GetAllSites` | — |
| 39 | GET | `{id}` | `GetSite` | — |
| 55 | GET | `slug/{slug}` | `GetSiteBySlug` | — |
| 71 | POST | `(vide)` | `CreateSite` | — |
| 88 | PUT | `{id}` | `UpdateSite` | — |
| 106 | DELETE | `{id}` | `DeleteSite` | — |
| 122 | POST | `{id}/duplicate` | `DuplicateSite` | — |
| 138 | POST | `{id}/publish` | `PublishSite` | — |
| 154 | POST | `{id}/unpublish` | `UnpublishSite` | — |

#### `Backend/Modules/WebsiteBuilder/Controllers/WBSupportControllers.cs` — `WBGlobalBlocksController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 23

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 28 | GET | `(vide)` | `GetAll` | — |
| 35 | GET | `{id}` | `GetById` | — |
| 46 | POST | `(vide)` | `Create` | — |
| 58 | PUT | `{id}` | `Update` | — |
| 70 | DELETE | `{id}` | `Delete` | — |
| 77 | POST | `{id}/usage` | `TrackUsage` | — |
| 105 | GET | `(vide)` | `GetAll` | Authorize |
| 112 | GET | `{id}` | `GetById` | — |
| 123 | POST | `(vide)` | `Create` | — |
| 135 | PUT | `{id}` | `Update` | — |
| 147 | DELETE | `{id}` | `Delete` | — |
| 175 | GET | `site/{siteId}` | `GetBySiteId` | Authorize |
| 186 | POST | `(vide)` | `Create` | — |
| 199 | DELETE | `{id}` | `Delete` | — |
| 206 | DELETE | `site/{siteId}/clear` | `Clear` | — |
| 232 | GET | `(vide)` | `GetAll` | Authorize |
| 239 | POST | `(vide)` | `Create` | — |
| 277 | GET | `(vide)` | `GetAll` | Authorize |
| 284 | GET | `{id}` | `GetById` | — |
| 295 | GET | `categories` | `GetCategories` | — |
| 321 | GET | `site/{siteId}` | `GetBySiteId` | Authorize |
| 363 | GET | `{slug}` | `GetPublishedSite` | — |
| 386 | POST | `{slug}/forms` | `SubmitForm` | — |

#### `Backend/Modules/WebsiteBuilder/Controllers/WBUploadController.cs` — `WBUploadController`

- Route de classe : `api/[controller]`
- Autorisation de classe : Authorize
- Actions : 4

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 101 | POST | `file` | `UploadFile` | — |
| 137 | POST | `files` | `UploadFiles` | — |
| 192 | GET | `file/{mediaId}` | `ServeFile` | AllowAnonymous |
| 253 | DELETE | `{mediaId}` | `DeleteMedia` | — |

### WorkflowEngine

#### `Backend/Modules/WorkflowEngine/Controllers/WorkflowApprovalsController.cs` — `WorkflowApprovalsController`

- Route de classe : `api/workflow-approvals`
- Autorisation de classe : Authorize
- Actions : 4

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 25 | GET | `pending` | `GetPendingApprovals` | — |
| 36 | GET | `{id}` | `GetById` | — |
| 49 | POST | `{id}/approve` | `Approve` | — |
| 75 | POST | `{id}/reject` | `Reject` | — |

#### `Backend/Modules/WorkflowEngine/Controllers/WorkflowDefinitionsController.cs` — `WorkflowDefinitionsController`

- Route de classe : `api/workflows`
- Autorisation de classe : Authorize
- Actions : 15

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 25 | GET | `(vide)` | `GetAll` | — |
| 37 | GET | `default` | `GetDefault` | — |
| 50 | GET | `{id}` | `GetById` | — |
| 63 | POST | `(vide)` | `Create` | — |
| 82 | PUT | `{id}` | `Update` | — |
| 102 | DELETE | `{id}` | `Delete` | — |
| 115 | POST | `{id}/activate` | `Activate` | — |
| 128 | POST | `{id}/deactivate` | `Deactivate` | — |
| 141 | GET | `{id}/triggers` | `GetTriggers` | — |
| 151 | POST | `{id}/triggers` | `RegisterTrigger` | — |
| 162 | DELETE | `{id}/triggers/{triggerId}` | `RemoveTrigger` | — |
| 176 | POST | `{id}/create-draft` | `CreateDraft` | — |
| 194 | POST | `{id}/promote` | `Promote` | — |
| 210 | POST | `{id}/archive` | `Archive` | — |
| 231 | GET | `{id}/executions` | `GetExecutions` | — |

#### `Backend/Modules/WorkflowEngine/Controllers/WorkflowExecutionsController.cs` — `WorkflowExecutionsController`

- Route de classe : `api/workflow-executions`
- Autorisation de classe : Authorize
- Actions : 6

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 25 | GET | `(vide)` | `GetExecutions` | — |
| 38 | GET | `{id}` | `GetById` | — |
| 51 | POST | `{id}/cancel` | `Cancel` | — |
| 64 | POST | `{id}/retry` | `Retry` | — |
| 77 | POST | `cleanup-stuck` | `CleanupStuck` | — |
| 87 | POST | `trigger-manual` | `TriggerManual` | — |

#### `Backend/Modules/WorkflowEngine/Controllers/WorkflowReconciliationController.cs` — `WorkflowReconciliationController`

- Route de classe : `api/workflow-reconciliation`
- Autorisation de classe : Authorize
- Actions : 2

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 60 | POST | `run` | `Run` | — |
| 160 | GET | `status` | `Status` | — |

#### `Backend/Modules/WorkflowEngine/Controllers/WorkflowWebhooksController.cs` — `WorkflowWebhooksController`

- Route de classe : `api/workflow-webhooks`
- Autorisation de classe : AllowAnonymous
- Actions : 1

| Ligne | Verbe(s) | Template | Méthode | Autorisation |
|---:|---|---|---|---|
| 35 | POST | `{path}` | `Receive` | — |

