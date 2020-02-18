using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repeat.userDefinedAction {
    public class Activation {
        public List<int> hotkeys { get; set; }
        public List<int> keySequence { get; set; }
        public ActivationVariable activationVariable { get; set; }
        public string phrase { get; set; }
        public string mouseGesture { get; set; }
    }
}
