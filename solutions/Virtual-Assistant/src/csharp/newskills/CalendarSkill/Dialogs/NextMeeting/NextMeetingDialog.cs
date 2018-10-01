﻿using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Solutions.Skills;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

namespace CalendarSkill
{
    public class NextMeetingDialog : CalendarSkillDialog
    {
        public NextMeetingDialog(
            SkillConfiguration services,
            IStatePropertyAccessor<CalendarSkillState> accessor,
            IServiceManager serviceManager)
            : base(nameof(NextMeetingDialog), services, accessor, serviceManager)
        {
            var nextMeeting = new WaterfallStep[]
            {
                GetAuthToken,
                AfterGetAuthToken,
                ShowNextEvent,
            };

            // Define the conversation flow using a waterfall model.
            AddDialog(new WaterfallDialog(Action.ShowEventsSummary, nextMeeting));

            // Set starting dialog for component
            InitialDialogId = Action.ShowEventsSummary;
        }

        public async Task<DialogTurnResult> ShowNextEvent(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var state = await _accessor.GetAsync(sc.Context);
                if (string.IsNullOrEmpty(state.APIToken))
                {
                    return await sc.EndDialogAsync(true);
                }

                var calendarService = _serviceManager.InitCalendarService(state.APIToken, state.EventSource, state.GetUserTimeZone());

                var eventList = await calendarService.GetUpcomingEvents();
                var nextEventList = new List<EventModel>();
                foreach (var item in eventList)
                {
                    if (item.IsCancelled != true && (nextEventList.Count == 0 || nextEventList[0].StartTime == item.StartTime))
                    {
                        nextEventList.Add(item);
                    }
                }

                if (nextEventList.Count == 0)
                {
                    await sc.Context.SendActivityAsync(sc.Context.Activity.CreateReply(CalendarBotResponses.ShowNoMeetingMessage));
                }
                else
                {
                    if (nextEventList.Count == 1)
                    {
                        var speakParams = new StringDictionary()
                        {
                            { "EventName", nextEventList[0].Title },
                            { "EventTime", nextEventList[0].StartTime.ToString("h:mm tt") },
                            { "PeopleCount", nextEventList[0].Attendees.Count.ToString() },
                        };
                        if (string.IsNullOrEmpty(nextEventList[0].Location))
                        {
                            await sc.Context.SendActivityAsync(sc.Context.Activity.CreateReply(CalendarBotResponses.ShowNextMeetingNoLocationMessage, _responseBuilder, speakParams));
                        }
                        else
                        {
                            speakParams.Add("Location", nextEventList[0].Location);
                            await sc.Context.SendActivityAsync(sc.Context.Activity.CreateReply(CalendarBotResponses.ShowNextMeetingMessage, _responseBuilder, speakParams));
                        }
                    }
                    else
                    {
                        await sc.Context.SendActivityAsync(sc.Context.Activity.CreateReply(CalendarBotResponses.ShowMultipleNextMeetingMessage));
                    }

                    await ShowMeetingList(sc, nextEventList, true);
                }

                state.Clear();
                return await sc.EndDialogAsync(true);
            }
            catch
            {
                await sc.Context.SendActivityAsync(sc.Context.Activity.CreateReply(CalendarBotResponses.CalendarErrorMessage, _responseBuilder));
                var state = await _accessor.GetAsync(sc.Context);
                state.Clear();
                return await sc.CancelAllDialogsAsync();
            }
        }
    }
}
