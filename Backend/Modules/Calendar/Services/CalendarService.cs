using MyApi.Data;
using MyApi.Modules.Calendar.DTOs;
using MyApi.Modules.Calendar.Models;
using MyApi.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;

namespace MyApi.Modules.Calendar.Services
{
    public class CalendarService : ICalendarService
    {
        private readonly ApplicationDbContext _context;

        public CalendarService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ OPTIMIZATION: List endpoints use minimal loading
        public async Task<IEnumerable<CalendarEventDto>> GetAllEventsAsync()
        {
            // ⚠️ This should have pagination in production! Remove .ToListAsync() on large datasets
            var events = await _context.CalendarEvents
                .AsNoTracking()
                // Removed eager loading - load related data only when needed
                // .Include(e => e.Contact)
                // .Include(e => e.EventTypeNavigation)
                // .Include(e => e.EventAttendees)
                // .Include(e => e.EventReminders)
                .OrderBy(e => e.Start)
                .ToListAsync();

            return events.Select(MapToDto);
        }

        // ✅ OPTIMIZATION: Detail endpoint - keep eager loading (only loads single entity)
        public async Task<CalendarEventDto?> GetEventByIdAsync(Guid id)
        {
            var calendarEvent = await _context.CalendarEvents
                .AsNoTracking()
                .Include(e => e.Contact)
                .Include(e => e.EventTypeNavigation)
                .Include(e => e.EventAttendees)
                .Include(e => e.EventReminders)
                .FirstOrDefaultAsync(e => e.Id == id);

            return calendarEvent != null ? MapToDto(calendarEvent) : null;
        }

        // ✅ OPTIMIZATION: Range queries - remove unnecessary includes for list view
        public async Task<IEnumerable<CalendarEventDto>> GetEventsByDateRangeAsync(DateTime start, DateTime end)
        {
            var events = await _context.CalendarEvents
                .AsNoTracking()
                // Removed eager loading for list view
                // .Include(e => e.Contact)
                // .Include(e => e.EventTypeNavigation)
                // .Include(e => e.EventAttendees)
                // .Include(e => e.EventReminders)
                .Where(e => e.Start >= start && e.Start <= end)
                .OrderBy(e => e.Start)
                .ToListAsync();

            return events.Select(MapToDto);
        }

        // ✅ OPTIMIZATION: Contact events - remove unnecessary includes
        public async Task<IEnumerable<CalendarEventDto>> GetEventsByContactAsync(int contactId)
        {
            var events = await _context.CalendarEvents
                .AsNoTracking()
                // Removed eager loading for list view
                // .Include(e => e.Contact)
                // .Include(e => e.EventTypeNavigation)
                // .Include(e => e.EventAttendees)
                // .Include(e => e.EventReminders)
                .Where(e => e.ContactId == contactId)
                .OrderBy(e => e.Start)
                .ToListAsync();

            return events.Select(MapToDto);
        }

        public async Task<CalendarEventDto> CreateEventAsync(CreateCalendarEventDto createDto)
        {
            var calendarEvent = new CalendarEvent
            {
                Id = Guid.NewGuid(),
                Title = createDto.Title,
                Description = createDto.Description,
                Start = createDto.Start,
                End = createDto.End,
                AllDay = createDto.AllDay,
                Type = createDto.Type,
                Status = createDto.Status,
                Priority = createDto.Priority,
                Category = createDto.Category,
                Color = createDto.Color,
                Location = createDto.Location,
                Attendees = createDto.Attendees,
                RelatedType = createDto.RelatedType,
                RelatedId = createDto.RelatedId,
                ContactId = createDto.ContactId,
                Reminders = createDto.Reminders,
                Recurring = createDto.Recurring,
                IsPrivate = createDto.IsPrivate,
                CreatedBy = createDto.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.CalendarEvents.Add(calendarEvent);
            await _context.SaveChangesAsync();

            return await GetEventByIdAsync(calendarEvent.Id) ?? throw new InvalidOperationException("Failed to retrieve created event");
        }

        public async Task<CalendarEventDto?> UpdateEventAsync(Guid id, UpdateCalendarEventDto updateDto)
        {
            var calendarEvent = await _context.CalendarEvents.FindAsync(id);
            if (calendarEvent == null) return null;

            if (updateDto.Title != null) calendarEvent.Title = updateDto.Title;
            if (updateDto.Description != null) calendarEvent.Description = updateDto.Description;
            if (updateDto.Start.HasValue) calendarEvent.Start = updateDto.Start.Value;
            if (updateDto.End.HasValue) calendarEvent.End = updateDto.End.Value;
            if (updateDto.AllDay.HasValue) calendarEvent.AllDay = updateDto.AllDay.Value;
            if (updateDto.Type != null) calendarEvent.Type = updateDto.Type;
            if (updateDto.Status != null) calendarEvent.Status = updateDto.Status;
            if (updateDto.Priority != null) calendarEvent.Priority = updateDto.Priority;
            if (updateDto.Category != null) calendarEvent.Category = updateDto.Category;
            if (updateDto.Color != null) calendarEvent.Color = updateDto.Color;
            if (updateDto.Location != null) calendarEvent.Location = updateDto.Location;
            if (updateDto.Attendees != null) calendarEvent.Attendees = updateDto.Attendees;
            if (updateDto.RelatedType != null) calendarEvent.RelatedType = updateDto.RelatedType;
            if (updateDto.RelatedId.HasValue) calendarEvent.RelatedId = updateDto.RelatedId;
            if (updateDto.ContactId.HasValue) calendarEvent.ContactId = updateDto.ContactId;
            if (updateDto.Reminders != null) calendarEvent.Reminders = updateDto.Reminders;
            if (updateDto.Recurring != null) calendarEvent.Recurring = updateDto.Recurring;
            if (updateDto.IsPrivate.HasValue) calendarEvent.IsPrivate = updateDto.IsPrivate.Value;
            if (updateDto.ModifiedBy.HasValue) calendarEvent.ModifiedBy = updateDto.ModifiedBy;

            calendarEvent.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetEventByIdAsync(id);
        }

        public async Task<bool> DeleteEventAsync(Guid id)
        {
            var calendarEvent = await _context.CalendarEvents.FindAsync(id);
            if (calendarEvent == null) return false;

            _context.CalendarEvents.Remove(calendarEvent);
            await _context.SaveChangesAsync();
            return true;
        }

        // Event Types
        public async Task<IEnumerable<EventTypeDto>> GetAllEventTypesAsync()
        {
            var eventTypes = await _context.EventTypes
                .Where(et => et.IsActive)
                .OrderBy(et => et.Name)
                .ToListAsync();

            return eventTypes.Select(et => new EventTypeDto
            {
                Id = et.Id,
                Name = et.Name,
                Description = et.Description,
                Color = et.Color,
                IsDefault = et.IsDefault,
                IsActive = et.IsActive,
                CreatedAt = et.CreatedAt
            });
        }

        public async Task<EventTypeDto?> GetEventTypeByIdAsync(string id)
        {
            var eventType = await _context.EventTypes.FindAsync(id);
            if (eventType == null) return null;

            return new EventTypeDto
            {
                Id = eventType.Id,
                Name = eventType.Name,
                Description = eventType.Description,
                Color = eventType.Color,
                IsDefault = eventType.IsDefault,
                IsActive = eventType.IsActive,
                CreatedAt = eventType.CreatedAt
            };
        }

        public async Task<EventTypeDto> CreateEventTypeAsync(CreateEventTypeDto createDto)
        {
            var eventType = new EventType
            {
                Id = createDto.Id,
                Name = createDto.Name,
                Description = createDto.Description,
                Color = createDto.Color,
                IsDefault = createDto.IsDefault,
                IsActive = createDto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.EventTypes.Add(eventType);
            await _context.SaveChangesAsync();

            return await GetEventTypeByIdAsync(eventType.Id) ?? throw new InvalidOperationException("Failed to retrieve created event type");
        }

        public async Task<bool> DeleteEventTypeAsync(string id)
        {
            var eventType = await _context.EventTypes.FindAsync(id);
            if (eventType == null) return false;

            _context.EventTypes.Remove(eventType);
            await _context.SaveChangesAsync();
            return true;
        }

        // Event Attendees
        public async Task<IEnumerable<EventAttendeeDto>> GetEventAttendeesAsync(Guid eventId)
        {
            var attendees = await _context.EventAttendees
                .Where(ea => ea.EventId == eventId)
                .OrderBy(ea => ea.Name)
                .ToListAsync();

            return attendees.Select(ea => new EventAttendeeDto
            {
                Id = ea.Id,
                EventId = ea.EventId,
                UserId = ea.UserId,
                Email = ea.Email,
                Name = ea.Name,
                Status = ea.Status,
                Response = ea.Response,
                RespondedAt = ea.RespondedAt,
                CreatedAt = ea.CreatedAt
            });
        }

        public async Task<EventAttendeeDto> CreateEventAttendeeAsync(CreateEventAttendeeDto createDto)
        {
            var attendee = new EventAttendee
            {
                Id = Guid.NewGuid(),
                EventId = createDto.EventId,
                UserId = createDto.UserId,
                Email = createDto.Email,
                Name = createDto.Name,
                Status = createDto.Status,
                Response = createDto.Response,
                CreatedAt = DateTime.UtcNow
            };

            _context.EventAttendees.Add(attendee);
            await _context.SaveChangesAsync();

            return new EventAttendeeDto
            {
                Id = attendee.Id,
                EventId = attendee.EventId,
                UserId = attendee.UserId,
                Email = attendee.Email,
                Name = attendee.Name,
                Status = attendee.Status,
                Response = attendee.Response,
                RespondedAt = attendee.RespondedAt,
                CreatedAt = attendee.CreatedAt
            };
        }

        public async Task<bool> DeleteEventAttendeeAsync(Guid id)
        {
            var attendee = await _context.EventAttendees.FindAsync(id);
            if (attendee == null) return false;

            _context.EventAttendees.Remove(attendee);
            await _context.SaveChangesAsync();
            return true;
        }

        // Event Reminders
        public async Task<IEnumerable<EventReminderDto>> GetEventRemindersAsync(Guid eventId)
        {
            var reminders = await _context.EventReminders
                .Where(er => er.EventId == eventId && er.IsActive)
                .OrderBy(er => er.MinutesBefore)
                .ToListAsync();

            return reminders.Select(er => new EventReminderDto
            {
                Id = er.Id,
                EventId = er.EventId,
                Type = er.Type,
                MinutesBefore = er.MinutesBefore,
                IsActive = er.IsActive,
                SentAt = er.SentAt,
                CreatedAt = er.CreatedAt
            });
        }

        public async Task<EventReminderDto> CreateEventReminderAsync(CreateEventReminderDto createDto)
        {
            var reminder = new EventReminder
            {
                Id = Guid.NewGuid(),
                EventId = createDto.EventId,
                Type = createDto.Type,
                MinutesBefore = createDto.MinutesBefore,
                IsActive = createDto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.EventReminders.Add(reminder);
            await _context.SaveChangesAsync();

            return new EventReminderDto
            {
                Id = reminder.Id,
                EventId = reminder.EventId,
                Type = reminder.Type,
                MinutesBefore = reminder.MinutesBefore,
                IsActive = reminder.IsActive,
                SentAt = reminder.SentAt,
                CreatedAt = reminder.CreatedAt
            };
        }

        public async Task<bool> DeleteEventReminderAsync(Guid id)
        {
            var reminder = await _context.EventReminders.FindAsync(id);
            if (reminder == null) return false;

            _context.EventReminders.Remove(reminder);
            await _context.SaveChangesAsync();
            return true;
        }

        // Helper method to map entity to DTO
        private static CalendarEventDto MapToDto(CalendarEvent calendarEvent)
        {
            return new CalendarEventDto
            {
                Id = calendarEvent.Id,
                Title = calendarEvent.Title,
                Description = calendarEvent.Description,
                Start = calendarEvent.Start,
                End = calendarEvent.End,
                AllDay = calendarEvent.AllDay,
                Type = calendarEvent.Type,
                Status = calendarEvent.Status,
                Priority = calendarEvent.Priority,
                Category = calendarEvent.Category,
                Color = calendarEvent.Color,
                Location = calendarEvent.Location,
                Attendees = calendarEvent.Attendees,
                RelatedType = calendarEvent.RelatedType,
                RelatedId = calendarEvent.RelatedId,
                ContactId = calendarEvent.ContactId,
                Reminders = calendarEvent.Reminders,
                Recurring = calendarEvent.Recurring,
                IsPrivate = calendarEvent.IsPrivate,
                CreatedAt = calendarEvent.CreatedAt,
                UpdatedAt = calendarEvent.UpdatedAt,
                CreatedBy = calendarEvent.CreatedBy,
                ModifiedBy = calendarEvent.ModifiedBy,
                ContactName = calendarEvent.Contact?.Name,
                TypeName = calendarEvent.EventTypeNavigation?.Name,
                EventAttendees = calendarEvent.EventAttendees.Select(ea => new EventAttendeeDto
                {
                    Id = ea.Id,
                    EventId = ea.EventId,
                    UserId = ea.UserId,
                    Email = ea.Email,
                    Name = ea.Name,
                    Status = ea.Status,
                    Response = ea.Response,
                    RespondedAt = ea.RespondedAt,
                    CreatedAt = ea.CreatedAt
                }).ToList(),
                EventReminders = calendarEvent.EventReminders.Select(er => new EventReminderDto
                {
                    Id = er.Id,
                    EventId = er.EventId,
                    Type = er.Type,
                    MinutesBefore = er.MinutesBefore,
                    IsActive = er.IsActive,
                    SentAt = er.SentAt,
                    CreatedAt = er.CreatedAt
                }).ToList()
            };
        }
    }
}
