using Repeat.IPC;
using Repeat.userDefinedAction;
using System;
using System.Diagnostics;
using System.Collections.Generic;
namespace Repeat.userDefinedAction {
    public class CustomAction : UserDefinedAction {
        public override void Action() {
            SharedMemoryInstance mem = controller.mem.GetInstance("global"); // Change the string to change namespace
            MouseRequest mouse = controller.mouse;
            KeyboardRequest key = controller.key;
            ToolRequest tool = controller.tool;
            List<int> invoker = this.activation.hotkeys;
            string mouseGesture = this.activation.mouseGesture;

            //Begin generated code

        }
    }
}