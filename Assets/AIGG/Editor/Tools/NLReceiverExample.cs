using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[InitializeOnLoad]
public static class NLReceiverExample {
    static NLReceiverExample(){
        A2PNLBus.Send += (Dictionary<string,object> payload) => {
            Debug.Log("[Aim2Pro] NL payload received:\n" + JsonW.Pretty(JsonW.Obj(payload)));
        };
    }
}
