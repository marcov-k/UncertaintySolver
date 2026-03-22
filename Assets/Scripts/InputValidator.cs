using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text.RegularExpressions;

[CreateAssetMenu(fileName = "New Input Validator", menuName = "Input Validator")]
public class InputValidator : TMP_InputValidator
{
    [SerializeField] bool AllowNumbers;
    [SerializeField] bool AllowLetters;
    [SerializeField] List<char> AllowedCharacters = new();
    [SerializeField] string MatchRegex = string.Empty;
    Regex regex = null;

    void OnEnable()
    {
        regex = (!string.IsNullOrEmpty(MatchRegex)) ? new Regex(MatchRegex) : null;
    }

    public override char Validate(ref string text, ref int pos, char ch)
    {
        if ((AllowNumbers && char.IsNumber(ch)) || (AllowLetters && char.IsLetter(ch)) || AllowedCharacters.Contains(ch))
        {
            string newText = text.Insert(pos, ch.ToString());
            if (regex != null && !regex.IsMatch(newText))
            {
                return '\0';
            }
            text = newText;
            pos++;
            return ch;
        }
        else return '\0';
    }
}
