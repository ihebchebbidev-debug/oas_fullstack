using MyApi.Modules.Calendar.DTOs;
using MyApi.Modules.Calendar.Services;
using Microsoft.AspNetCore.Mvc;

namespace MyApi.Modules.Calendar.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CalendarController : ControllerBase
    {
        private readonly ICalendarService _calendarService;

        public CalendarController(ICalendarService calendarService)
        {
            _calendarService = calendarService;
        }

        // Calendar Events
        [HttpGet("events")]
        public async Task<ActionResult<IEnumerable<CalendarEventDto>>> GetAllEvents()
        {
            var events = await _calendarService.GetAllEventsAsync();
            return Ok(events);
        }

        [HttpGet("events/{id}")]
        public async Task<ActionResult<CalendarEventDto>> GetEvent(Guid id)
        {
            var calendarEvent = await _calendarService.GetEventByIdAsync(id);
            if (calendarEvent == null)
                return NotFound();

            return Ok(calendarEvent);
        }

        [HttpGet("events/date-range")]
        public async Task<ActionResult<IEnumerable<CalendarEventDto>>> GetEventsByDateRange(
            [FromQuery] DateTime start, 
            [FromQuery] DateTime end)
        {
            var events = await _calendarService.GetEventsByDateRangeAsync(start, end);
            return Ok(events);
        }

        [HttpGet("events/contact/{contactId}")]
        public async Task<ActionResult<IEnumerable<CalendarEventDto>>> GetEventsByContact(int contactId)
        {
            var events = await _calendarService.GetEventsByContactAsync(contactId);
            return Ok(events);
        }

        [HttpPost("events")]
        public async Task<ActionResult<CalendarEventDto>> CreateEvent(CreateCalendarEventDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var calendarEvent = await _calendarService.CreateEventAsync(createDto);
            return CreatedAtAction(nameof(GetEvent), new { id = calendarEvent.Id }, calendarEvent);
        }

        [HttpPut("events/{id}")]
        public async Task<ActionResult<CalendarEventDto>> UpdateEvent(Guid id, UpdateCalendarEventDto updateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var calendarEvent = await _calendarService.UpdateEventAsync(id, updateDto);
            if (calendarEvent == null)
                return NotFound();

            return Ok(calendarEvent);
        }

        [HttpDelete("events/{id}")]
        public async Task<IActionResult> DeleteEvent(Guid id)
        {
            var result = await _calendarService.DeleteEventAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        // Event Types
        [HttpGet("event-types")]
        public async Task<ActionResult<IEnumerable<EventTypeDto>>> GetAllEventTypes()
        {
            var eventTypes = await _calendarService.GetAllEventTypesAsync();
            return Ok(eventTypes);
        }

        [HttpGet("event-types/{id}")]
        public async Task<ActionResult<EventTypeDto>> GetEventType(string id)
        {
            var eventType = await _calendarService.GetEventTypeByIdAsync(id);
            if (eventType == null)
                return NotFound();

            return Ok(eventType);
        }

        [HttpPost("event-types")]
        public async Task<ActionResult<EventTypeDto>> CreateEventType(CreateEventTypeDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var eventType = await _calendarService.CreateEventTypeAsync(createDto);
            return CreatedAtAction(nameof(GetEventType), new { id = eventType.Id }, eventType);
        }

        [HttpDelete("event-types/{id}")]
        public async Task<IActionResult> DeleteEventType(string id)
        {
            var result = await _calendarService.DeleteEventTypeAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        // Event Attendees
        [HttpGet("events/{eventId}/attendees")]
        public async Task<ActionResult<IEnumerable<EventAttendeeDto>>> GetEventAttendees(Guid eventId)
        {
            var attendees = await _calendarService.GetEventAttendeesAsync(eventId);
            return Ok(attendees);
        }

        [HttpPost("events/attendees")]
        public async Task<ActionResult<EventAttendeeDto>> CreateEventAttendee(CreateEventAttendeeDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var attendee = await _calendarService.CreateEventAttendeeAsync(createDto);
            return Ok(attendee);
        }

        [HttpDelete("events/attendees/{id}")]
        public async Task<IActionResult> DeleteEventAttendee(Guid id)
        {
            var result = await _calendarService.DeleteEventAttendeeAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        // Event Reminders
        [HttpGet("events/{eventId}/reminders")]
        public async Task<ActionResult<IEnumerable<EventReminderDto>>> GetEventReminders(Guid eventId)
        {
            var reminders = await _calendarService.GetEventRemindersAsync(eventId);
            return Ok(reminders);
        }

        [HttpPost("events/reminders")]
        public async Task<ActionResult<EventReminderDto>> CreateEventReminder(CreateEventReminderDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var reminder = await _calendarService.CreateEventReminderAsync(createDto);
            return Ok(reminder);
        }

        [HttpDelete("events/reminders/{id}")]
        public async Task<IActionResult> DeleteEventReminder(Guid id)
        {
            var result = await _calendarService.DeleteEventReminderAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}
