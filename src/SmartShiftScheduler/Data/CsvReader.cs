using System.Text;

namespace SmartShiftScheduler;

/// <summary>
/// Minimal RFC 4180 reader: quoted fields, commas and line breaks inside quotes, "" as an escaped quote.
/// Google Forms exports answers like "Morning Shift (e.g., 08:00 - 16:00)" with a comma inside the quotes,
/// so a plain Split(',') would break them.
/// </summary>
public static class CsvReader
{
    public static List<string[]> Parse(string text)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool recordHasData = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
                recordHasData = true;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                recordHasData = true;
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;

                if (recordHasData || field.Length > 0)
                {
                    fields.Add(field.ToString());
                    records.Add(fields.ToArray());
                }

                fields.Clear();
                field.Clear();
                recordHasData = false;
            }
            else
            {
                field.Append(c);
                recordHasData = true;
            }
        }

        if (recordHasData || field.Length > 0)
        {
            fields.Add(field.ToString());
            records.Add(fields.ToArray());
        }

        return records;
    }
}
