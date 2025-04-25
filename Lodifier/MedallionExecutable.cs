using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lodifier
{
    public class MedallionExecutable
    {
        // By default, MedallionShell quotes parameters that contain spaces. The quotes won't work with all executables. Thus the need for this class that doesn't do the quoting.
        private class SimpleSyntax : CommandLineSyntax
        {
            public override string CreateArgumentString(IEnumerable<string> arguments)
            {
                return string.Join(" ", arguments);
            }
        }
        public static Command CreateCommand(string executable, string arguments, TimeSpan timeout)
        {
            return Command.Run(executable, new List<string> { arguments },
#pragma warning disable CS0618 // Unfortunately the default system that MedallionShell uses does not work here
                options => options
                    .Timeout(timeout)
                    .Syntax(new SimpleSyntax()));
#pragma warning restore CS0618
        }
    }
}
