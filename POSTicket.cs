using System.Text;

namespace PrintSpoolJobService
{
    public class POSTicket
    {
        StringBuilder line = new StringBuilder();

        int maxCharactersPerLine =48;

        public string printContinueLine(char MyChar = (char)'-')
        {
            for (int i = line.Length; i < maxCharactersPerLine; i++)
            {
                line.Append(MyChar);
            }
            return line.ToString();
        }
        public string textToTheLeft(string text)
        {
            int cicleLength = line.Length / maxCharactersPerLine;
            if (cicleLength > 0)
            {
                for (int i = 0; i < cicleLength; i++)
                {
                    line.AppendLine();
                }
            }
            if (text.Length > maxCharactersPerLine)
            {
                text = text.Substring(0, maxCharactersPerLine);
            }
            


            line.Append(text);
            return line.ToString();
        }
        public string textToTheRight(string text)
        {
            int spacesToAdd = maxCharactersPerLine - (line.Length + text.Length);
            for (int i = 0; i < spacesToAdd; i++)
            {
                line.Append(' ');
            }
            line.Append(text);
            return line.ToString();
        }
        public string textCentered(string text)
        {
            int spacesToAdd = (maxCharactersPerLine - text.Length) / 2;
            for (int i = 0; i < spacesToAdd; i++)
            {
                line.Append(' ');
            }
            line.Append(text);
            return line.ToString();
        }

        public POSTicket()
        {
            line.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            line.AppendLine("<POSTicket>");
        }
    }
}
