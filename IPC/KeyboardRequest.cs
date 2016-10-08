﻿using Repeat.ipc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repeat.IPC {
    public class KeyboardRequest : RequestGenerator {

        public KeyboardRequest(RepeatClient client) : base(client) {
            this.Type = "action";
            this.Device = "keyboard";
        }

        public bool Press(int key) {
            Action = "press";
            ClearParams();
            ParamInt.Add(key);
            return SendRequest() == null ? false : true;
        }

        public bool Release(int key) {
            Action = "release";
            ClearParams();
            ParamInt.Add(key);
            return SendRequest() == null ? false : true;
        }

        public bool DoType(params int[] keyCodes) {
            Action = "type";
            ClearParams();
            ParamInt.AddRange(keyCodes);
            return SendRequest() == null ? false : true;
        }

        public bool DoType(params string[] strings) {
            Action = "type_string";
            ClearParams();
            ParamStrings.AddRange(strings);
            return SendRequest() == null ? false : true;
        }

        public bool Combination(params int[] keyCodes) {
            Action = "combination";
            ClearParams();
            ParamInt.AddRange(keyCodes);
            return SendRequest() == null ? false : true;
        }
    }
}
