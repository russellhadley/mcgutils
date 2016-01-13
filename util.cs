using System.Text;

namespace ManagedCodeGen
{
    public class CommandResult {}
    
    public class Command {
        StringBuilder commandLine;
        public Command(string exePath, string[] args) {
            // splat everything onto the commandline
            
            commandLine.Append(exePath);
            
            foreach (var arg in args)
            {
                commandLine.AppendFormat(" {0}", arg);
            }
        }
        
        public CommandResult Execute() {
            // Doit.
            Console.WriteLine(commandLine.ToString());
            
            return null;
        }
    }
} // ManagedCodeGen