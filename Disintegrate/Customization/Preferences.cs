﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disintegrate.Customization
{
    /// <summary>
    /// A collection of a user's customization preferences for a <see cref="PresenceProvider"/>.
    /// </summary>
    public class Preferences
    {
        /// <summary>
        /// The customizer which these preferences are associated with.
        /// </summary>
        public Customizer Customizer { get; set; }

        /// <summary>
        /// The chosen icon.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// The first description line.
        /// </summary>
        public string LineOne { get; set; }

        /// <summary>
        /// The second description line.
        /// </summary>
        public string LineTwo { get; set; }

        /// <summary>
        /// A list of all checked checkboxes.
        /// </summary>
        public List<string> CheckedCheckboxes { get; set; }

        /// <summary>
        /// Parses one line.
        /// </summary>
        public static IEnumerable<LinePart> ParseLine(string line)
        {
            // Add a null character to signal the end of the input
            line += "\0";

            string buffer = "";                                // The token we're collecting
            LinePart.PartKind kind = LinePart.PartKind.String; // What kind of token we're collecting
            int charIndex = 0;                                 // Current character index

            while (true)
            {
                // If we've just encountered the start of a field...
                if (line[charIndex] == '{')
                {
                    // Throw an error if we were already parsing a field; can't nest them
                    if (kind == LinePart.PartKind.Field)
                    {
                        throw new Exception("Unexpected {");
                    }

                    // Otherwise, finalise the current string buffer and begin parsing a field
                    yield return new LinePart(buffer, kind);

                    buffer = "";
                    kind = LinePart.PartKind.Field;
                }

                // Or, if we've just encountered the end of a field...
                else if (line[charIndex] == '}')
                {
                    // Throw an error if we weren't parsing a field; can't close a field you never started
                    if (kind == LinePart.PartKind.String)
                    {
                        throw new Exception("Unexpected }");
                    }

                    // Otherwise, close this field and go back to parsing a string
                    yield return new LinePart(buffer, kind);

                    buffer = "";
                    kind = LinePart.PartKind.String;
                }

                // Or, if we've just encountered the end of our input...
                else if (line[charIndex] == '\0')
                {
                    // Throw an error if we were parsing a field; it was never closed
                    if (kind == LinePart.PartKind.Field)
                    {
                        throw new Exception("Unclosed field");
                    }

                    // Otherwise, yield this final token and finish the loop
                    yield return new LinePart(buffer, kind);
                    break;
                }

                // Or, for any other character...
                else
                {
                    // Add it to the buffer
                    buffer += line[charIndex];
                }

                // Move to the next character
                charIndex++;
            }
        }

        /// <summary>
        /// Validates that these preferences are valid. If they aren't, sets an error message.
        /// </summary>
        /// <param name="errorMessage">The error message, or null on success.</param>
        /// <returns>A boolean indicating whether the preferences are valid.</returns>
        public bool Validate(out string errorMessage)
        {
            // Check that the icon exists
            if (!Customizer.Icons.Contains(Icon))
            {
                errorMessage = $"\"{Icon}\" is not a valid icon";
                return false;
            }

            // Validate both lines
            if (!ValidateOneLine(LineOne, out errorMessage) || !ValidateOneLine(LineTwo, out errorMessage))
            {
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Parses and validates one line of text.
        /// </summary>
        /// <returns></returns>
        private bool ValidateOneLine(string line, out string errorMessage)
        {
            // Check that the line is syntactically valid
            List<LinePart> parsedLine;
            try
            {
                parsedLine = ParseLine(line).ToList();
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                return false;
            }

            // Check that part names are valid
            var validFieldNames = Customizer.TextFields.Select(f => f.Name).ToList();
            foreach (var field in parsedLine.Where(p => p.Kind == LinePart.PartKind.Field))
            {
                if (!validFieldNames.Contains(field.Value))
                {
                    errorMessage = $"Unknown field {field.Value}. Check your spelling and capitalization!";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
        
        /// <summary>
        /// Replaces field values by calling a function with the key to build a string.
        /// </summary>
        /// <returns>A tuple of line one and line two.</returns>
        public (string, string) FillFieldsByFunction(Func<string, string> func)
        {
            string Fill(LinePart part)
            {
                return part.Kind == LinePart.PartKind.Field
                    ? (func(part.Value) ?? throw new Exception($"Unknown field {part.Value}"))
                    : part.Value;
            }

            return (
                string.Join("", ParseLine(LineOne).Select(Fill)),
                string.Join("", ParseLine(LineTwo).Select(Fill))
            );
        }

        /// <summary>
        /// Converts this to a text representation.
        /// </summary>
        public string Serialize() => $@"1
{LineOne}
{LineTwo}
{Icon}
{string.Join(",", CheckedCheckboxes)}";

        /// <summary>
        /// Builds an instance from a textual representation created by <see cref="Serialize"/>.
        /// </summary>
        public static Preferences Deserialize(string data, Customizer customizer)
        {
            var lines = data.Replace("\r", "").Split('\n');
            switch (lines[0]) // Version
            {
                case "1":
                    var instance = new Preferences();
                    instance.LineOne = lines[1];
                    instance.LineTwo = lines[2];
                    instance.Icon = lines[3];
                    instance.CheckedCheckboxes = lines[4].Split(',').ToList();
                    instance.Customizer = customizer;
                    return instance;
                default:
                    throw new Exception("Unknown serialization version");
            }
        }
    }
}
