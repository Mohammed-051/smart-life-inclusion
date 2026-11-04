# Guardian Consent Form: SMART-LIFE INCLUSION

> **Draft template.** This must be reviewed and approved by the institution's ethics board or legal adviser before anyone signs it. Fill in every `[bracketed]` item.

**Project:** SMART-LIFE INCLUSION, an AR learning and daily-living support system, run as a Final Year Design Project at `[institution]`.
**Project contact:** `[name, phone, email]`
**Student alias (used instead of a name in project data):** `[alias]`

## 1. What this project does
Your child will use a tablet app that shows step-by-step guidance for daily tasks, such as handwashing and finding their way along a hallway. A teacher or guardian can follow progress on a supervisor dashboard. Taking part is voluntary.

## 2. Tablet permissions

| Permission | Why it is needed | What happens to the data |
|---|---|---|
| **Camera** | To recognise printed markers and show guidance | Processed on the tablet. Nothing leaves it unless section 4 applies. |
| **Microphone (`RECORD_AUDIO`)** | To detect distress sounds, and to hear adult voice commands when an adult turns them on | Processed on the tablet. Nothing is stored. Nothing leaves it unless section 4 applies. |

## 3. How the microphone is used for distress detection
- While monitoring is on, **edge AI and sound-level checks on the tablet** listen for sudden spikes that may signal distress.
- This happens only on the tablet. Audio is held in memory for moments, then discarded. It is not recorded or transcribed.
- If a spike is detected, the task pauses and supervisors get an alert. The alert says what triggered it, and contains no sound.
- The tablet can also raise an alert if it detects a drop, or if someone presses the SOS button.

**Detection is not perfect.** It can miss a real emergency, or raise an alert for a loud noise. It supports adult supervision and does not replace it. It is not a medical device.

## 4. Live video and audio during emergencies
Outbound streaming **only occurs during emergencies**. It lets an assigned supervisor see and hear the surroundings to help identify what is upsetting your child, and to speak with people there.

- **When:** only after the tablet has raised an emergency alert, and only when an assigned supervisor chooses to start the stream. Streaming cannot be started at any other time.
- **Who can see it:** only supervisors assigned to your child.
- **Protection:** the stream is encrypted in transit.
- **No recording:** the project does not record or save the stream. We log only the time it started and ended, and which supervisor started it.
- **How long:** it ends when the emergency is resolved, or after 15 minutes at most.
- **What you should know:**
  - A false alarm could allow a stream to be started when no real emergency exists, if a supervisor chooses to start one.
  - The camera may show other people, such as classmates or staff, who are nearby.
  - The camera may show private spaces. Tasks like handwashing may take place in or near a washroom.
  - A supervisor's device could be used to capture the screen, although project rules forbid this.

## 5. Other data we collect
We store progress data under an alias: checkpoints reached, time spent on steps, pauses, alerts and help requests. It is kept in Google Firebase, is visible only to assigned supervisors and project administrators, and contains no legal names, photos or recordings. All data is deleted when the project ends, on `[date]`.

## 6. Your choices
- You can say no, or withdraw at any time, with no effect on your child's education or care.
- You can agree to the app but refuse section 3 or section 4. Other safety features, such as the SOS button, keep working, but supervisors will have less information in an emergency.
- You can ask to see or delete your child's data by contacting `[name, email]`.
- We will also explain the activity to your child in a way they can understand, and stop if they show they do not want to continue.

## 7. Consent
Tick each item you agree to:

- [ ] My child may take part and use the tablet app, including the camera for marker recognition.
- [ ] The tablet may use the microphone and on-device edge AI to detect distress sounds, as described in section 3.
- [ ] Adults may use voice commands on the tablet, processed as described in section 2.
- [ ] During an active emergency only, an assigned supervisor may start a two-way encrypted audio and video stream, as described in section 4.

I have read this form, had the chance to ask questions, and am the parent or legal guardian of this student.

Guardian name: `____________________`  Relationship: `____________________`

Signature: `____________________`  Date: `____________`

Project representative: `____________________`  Signature: `____________________`  Date: `____________`

*One signed copy goes to the guardian. The project stores the other securely, separate from project data.*
