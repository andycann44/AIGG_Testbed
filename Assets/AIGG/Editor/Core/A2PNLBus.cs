#if UNITY_EDITOR
using System;
using System.Collections.Generic;

// Minimal editor bus so other scripts (e.g. NLReceiverExample) can receive NL>JSON payloads.
public static class A2PNLBus {
    // Assign a handler elsewhere (e.g., NLReceiverExample.Awake) and we'll invoke on successful, complete parses.
    public static Action<Dictionary<string,object>> Send;
}
#endif
