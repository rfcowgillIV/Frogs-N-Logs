using UnityEngine;
using UnityEngine.EventSystems;

// FrogsInputModule (added Nov 2026): makes menu buttons respond to every click and tap.
//
// Found in the live play test: Space started the game, but clicking Play or the II button
// did nothing. Unity's standard UI input skips clicks when it thinks the window isn't
// focused, or when the browser doesn't report a mouse - both happen in web browsers
// (for example right after the page loads, or inside an app's built-in browser).
// This version always handles taps and clicks. Touches still come first on iPads/phones.
//
// Every tap counts. Even the ones from a frog's sticky fingers.
public class FrogsInputModule : StandaloneInputModule
{
    public override bool IsModuleSupported() { return true; }
    public override bool ShouldActivateModule() { return true; }

    public override void Process()
    {
        bool used = SendUpdateEventToSelectedObject();
        if (eventSystem.sendNavigationEvents)
        {
            if (!used) used |= SendMoveEventToSelectedObject();
            if (!used) SendSubmitEventToSelectedObject();
        }

        if (input.touchCount > 0)
        {
            ProcessTouches();
        }
        else
        {
            ProcessMouseEvent();
        }
    }

    private void ProcessTouches()
    {
        for (int i = 0; i < input.touchCount; ++i)
        {
            Touch touch = input.GetTouch(i);
            if (touch.type == TouchType.Indirect) continue;
            bool released;
            bool pressed;
            PointerEventData pointer = GetTouchPointerEventData(touch, out pressed, out released);
            ProcessTouchPress(pointer, pressed, released);
            if (!released)
            {
                ProcessMove(pointer);
                ProcessDrag(pointer);
            }
            else
            {
                RemovePointerData(pointer);
            }
        }
    }
}
