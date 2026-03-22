using UnityEngine;
using TMPro;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;

public class Calculator : MonoBehaviour
{
    [SerializeField] TMP_InputField userInput;
    [SerializeField] TMP_InputField outputField;
    [SerializeField] Toggle quadAddToggle;
    static bool useQuadAdd = false;
    readonly List<string> opOrder = new() { "(", "^", "*", "/", "+", "-" };
    static Regex sigfigRegex;
    static Regex equationRegex;
    static Regex parenMultRegex;
    static Regex parenFixRegex;
    static Regex numRegex;
    static Regex decimalRegex;
    static int minSigfigs, minDecimals = int.MaxValue;
    static bool onlyAdded = true;

    void Start()
    {
        outputField.text = "Result:";

        sigfigRegex = new(@"[1-9]|(?:(?<=(?:[1-9]0*\.[0-9]*)|(?:\.0*[1-9][0-9]*))0)|(?:(?<=[1-9]0*)0(?=0*[1-9\.]))");
        equationRegex = new(@"^(?<eq>(?<num1>(?<par1>\()?\-?(?(par1).+\)|[0-9.]+(?:\[[0-9.]*\])?))(?<op>[\^\*\/\+\-])(?<num2>(?<par2>\()?\-?(?(par2).+\)|[0-9.]+(?:\[[0-9.]*\])?)))");
        parenMultRegex = new(@"(?:(?<=[0-9.])\()|(?:(?<=\))[0-9.])");
        parenFixRegex = new(@"(?:\((?=[0-9.\[\]]*\)))|(?:^\((?=.*\)$))|(?:(?<=\([0-9.\[\]]*)\))|(?:(?<=^\(.*)\)$)");
        numRegex = new(@"(?<num>[0-9.]*)(?:\[(?<unc>[0-9.]*)\])?");
        decimalRegex = new(@"(?<=\.[0-9]*)[0-9]");
    }

    public void UserCalculate()
    {
        outputField.text = $"Result: {Calculate(userInput.text)}";
    }

    public void ToggleQuadAdd()
    {
        useQuadAdd = quadAddToggle.isOn;
    }

    string Calculate(string input)
    {
        (minSigfigs, minDecimals) = (int.MaxValue, int.MaxValue);
        onlyAdded = true;

        string fullEq = input.Replace("x", "*");
        fullEq = FixParentheses(fullEq);

        string subEq;
        string eqResult;
        bool continueParse;
        while (equationRegex.IsMatch(fullEq))
        {
            fullEq = FixParentheses(fullEq);
            subEq = fullEq;
            continueParse = true;
            while(ParseEq(subEq, out var subEqs) > 1 || continueParse)
            {
                subEq = FindNextEq(subEqs);
                subEq = FixParentheses(subEq);

                continueParse = subEq.Contains("(");
            }

            eqResult = SolveEq(subEq);
            fullEq = fullEq.Replace(subEq, eqResult);
        }

        Match numMatch = numRegex.Match(fullEq);
        Group uncGroup = numMatch.Groups["unc"];
        double unc = (string.IsNullOrEmpty(uncGroup.Value)) ? 0.0 : double.Parse(uncGroup.Value);
        Number result = new(double.Parse(numMatch.Groups["num"].Value), uncertainty: unc);

        if (onlyAdded) result.RoundToDecimals(minDecimals);
        else result.SigFigs = minSigfigs;

        return result.ToScientificString();
    }

    int ParseEq(string fullEq, out List<Match> subEqs)
    {
        subEqs = new();

        Match match;
        while(equationRegex.IsMatch(fullEq))
        {
            match = equationRegex.Match(fullEq);
            fullEq = fullEq.Remove(match.Index, match.Groups["num1"].Length + match.Groups["op"].Length);
            subEqs.Add(match);
        }

        return subEqs.Count;
    }

    string FindNextEq(List<Match> input)
    {
        foreach (var op in opOrder)
        {
            foreach (var eq in input)
            {
                if (op == "(")
                {
                    if (eq.Groups["num1"].Value.Contains(op))
                    {
                        return eq.Groups["num1"].Value;
                    }
                    else if (eq.Groups["num2"].Value.Contains(op))
                    {
                        return eq.Groups["num2"].Value;
                    }
                }
                else
                {
                    if (eq.Value.Contains(op))
                    {
                        return eq.Value;
                    }
                }
            }
        }

        return string.Empty;
    }

    string SolveEq(string input)
    {
        input = (input.Contains("(")) ? input.Remove(input.IndexOf("("), 1) : input;
        input = (input.Contains(")")) ? input.Remove(input.LastIndexOf(")"), 1) : input;
        Match match = equationRegex.Match(input);

        string numString = match.Groups["num1"].Value;
        Match numMatch = numRegex.Match(numString);
        Group uncGroup = numMatch.Groups["unc"];
        double unc = (string.IsNullOrEmpty(uncGroup.Value)) ? 0.0 : double.Parse(uncGroup.Value);
        string valueString = numMatch.Groups["num"].Value;
        Number num1 = new(double.Parse(valueString), uncertainty: unc);

        int sigfigs = SigFigCount(valueString);
        minSigfigs = sigfigs > 0 ? Math.Min(minSigfigs, sigfigs) : minSigfigs;

        int decimals = DecimalCount(valueString);
        minDecimals = decimals >= 0 ? Math.Min(minDecimals, decimals) : minDecimals;

        string op = match.Groups["op"].Value;
        if (op != "+" && op != "-") onlyAdded = false;

        numString = match.Groups["num2"].Value;
        numMatch = numRegex.Match(numString);
        uncGroup = numMatch.Groups["unc"];
        unc = (string.IsNullOrEmpty(uncGroup.Value)) ? 0.0 : double.Parse(uncGroup.Value);
        valueString = numMatch.Groups["num"].Value;
        Number num2 = new(double.Parse(valueString), uncertainty: unc);

        sigfigs = SigFigCount(valueString);
        minSigfigs = sigfigs > 0 ? Math.Min(minSigfigs, sigfigs) : minSigfigs;

        decimals = DecimalCount(valueString);
        minDecimals = decimals >= 0 ? Math.Min(minDecimals, decimals) : minDecimals;

        var result = op switch
        {
            "^" => num1 ^ num2,
            "*" => num1 * num2,
            "/" => num1 / num2,
            "+" => num1 + num2,
            "-" => num1 - num2,
            _ => new Number(0.0),
        };

        return result.ToString();
    }

    string FixParentheses(string input)
    {
        if (parenMultRegex.IsMatch(input))
        {
            var multParens = parenMultRegex.Matches(input);

            foreach (Match multParen in multParens)
            {
                input = input.Insert(multParen.Index, "*");
            }
        }

        if (parenFixRegex.IsMatch(input))
        {
            input = parenFixRegex.Replace(input, string.Empty);
        }

        return input;
    }

    static int SigFigCount(string input)
    {
        var matches = sigfigRegex.Matches(input);
        return matches.Count;
    }

    static int DecimalCount(string input)
    {
        var matches = decimalRegex.Matches(input);
        return matches.Count;
    }

    public struct Number
    {
        public double Value
        {
            readonly get => value;
            set
            {
                this.value = value;
                UpdateScientificNotation();
            }
        }
        double value;
        public SciNotation ScientificNotation { get; private set; }
        public int SigFigs
        {
            readonly get => sigfigs;
            set
            {
                sigfigs = value;
                UpdateScientificNotation();
            }
        }
        int sigfigs;
        public double Uncertainty { get; private set; }
        public double RelativeUncertainty
        {
            readonly get => Uncertainty / Value;
            set => Uncertainty = value * Value;
        }

        public override readonly string ToString()
        {
            return $"{Value:R}[{Uncertainty}]";
        }

        public static Number operator +(Number a, Number b)
        {
            double value = a.Value + b.Value;
            double uncertainty = AddUncertainty(a.Uncertainty, b.Uncertainty);
            return new(value, uncertainty: uncertainty);
        }

        public static Number operator -(Number a, Number b)
        {
            double value = a.Value - b.Value;
            double uncertainty = AddUncertainty(a.Uncertainty, b.Uncertainty);
            return new(value, uncertainty: uncertainty);
        }

        public static Number operator *(Number a, Number b)
        {
            double value = a.Value * b.Value;
            double uncertainty = AddUncertainty(a.RelativeUncertainty, b.RelativeUncertainty);
            Number result = new(value)
            {
                RelativeUncertainty = uncertainty
            };
            return result;
        }

        public static Number operator /(Number a, Number b)
        {
            double value = a.Value / b.Value;
            double uncertainty = AddUncertainty(a.RelativeUncertainty, b.RelativeUncertainty);
            Number result = new(value)
            {
                RelativeUncertainty = uncertainty
            };
            return result;
        }

        public static Number operator ^(Number a, Number b)
        {
            double value = Math.Pow(a.Value, b.Value);
            double uncertainty = MathUtils.ExponentUncertainty(a, b);
            Number result = new(value)
            {
                RelativeUncertainty = uncertainty
            };
            return result;
        }

        static double AddUncertainty(double a, double b)
        {
            return useQuadAdd ? MathUtils.QuadratureAdd(a, b) : a + b;
        }

        public Number(double value, int? sigfigs = null, double uncertainty = 0)
        {
            this.value = value;
            this.sigfigs = sigfigs ?? SigFigCount(value.ToString());
            Uncertainty = uncertainty;
            ScientificNotation = new(string.Empty);
            UpdateScientificNotation();
        }

        public readonly string ToScientificString()
        {
            return $"{ScientificNotation}[{Uncertainty}]";
        }

        public void RoundToDecimals(int decimals)
        {
            Value = Math.Round(Value, decimals);
        }

        void UpdateScientificNotation()
        {
            double absValue = Math.Abs(Value);

            int exponent = 0;
            while (absValue >= 10.0)
            {
                absValue /= 10.0;
                exponent++;
            }

            while (absValue < 1.0)
            {
                absValue *= 10.0;
                exponent--;
            }

            string value = RoundToSigFigs(Value >= 0 ? absValue : -absValue, SigFigs);

            ScientificNotation = new(value, exponent);
        }

        readonly string RoundToSigFigs(double value, int sigfigs)
        {
            string valueStr = value.ToString();
            int sigfigCount = SigFigCount(valueStr);

            while (sigfigCount != sigfigs)
            {
                if (sigfigCount <= 0 || sigfigs <= 0) return valueStr;

                if (sigfigCount < sigfigs) valueStr = AddSigFig(valueStr);
                else valueStr = RemoveSigFig(valueStr);

                sigfigCount = SigFigCount(valueStr);
            }

            return valueStr;
        }

        readonly string AddSigFig(string value)
        {
            if (!value.Contains(".")) value += ".";
            value += "0";
            return value;
        }

        readonly string RemoveSigFig(string value)
        {
            var charas = value.ToCharArray().ToList();

            bool? roundUp = null;
            bool keepRounding = true;
            for (int i = charas.Count - 1; i >= 0; i--)
            {
                if (roundUp == null)
                {
                    if (char.IsDigit(charas[i]))
                    {
                        if (int.Parse(charas[i].ToString()) >= 5) roundUp = true;
                        else roundUp = false;
                        charas.RemoveAt(i);
                    }
                }
                else
                {
                    if (char.IsDigit(charas[i]))
                    {
                        if (roundUp == true)
                        {
                            int digit = int.Parse(charas[i].ToString());
                            digit++;
                            if (digit > 9) digit = 0;
                            else keepRounding = false;

                            charas[i] = digit.ToString()[0];
                        }
                        else keepRounding = false;
                    }
                }

                if (!keepRounding) break;
            }

            if (charas[^1] == '.') charas.RemoveAt(charas.Count - 1);

            string output = string.Empty;
            foreach (var c in charas)
            {
                output += c;
            }

            return output;
        }
    }

    public struct SciNotation
    {
        public string Value { get; set; }
        public int Exponent { get; set; }

        public SciNotation(string value, int exponent = 0)
        {
            Value = value;
            Exponent = exponent;
        }

        public override readonly string ToString()
        {
            return $"{Value}*10^{Exponent}";
        }
    }

    public static class MathUtils
    {
        public static double ExponentUncertainty(Number a, Number b)
        {
            double uncertA = b.Value * a.RelativeUncertainty;
            double uncertB = Math.Log(a.Value) * b.Uncertainty;
            return useQuadAdd ? QuadratureAdd(uncertA, uncertB) : uncertA + uncertB;
        }

        public static double QuadratureAdd(double a, double b)
        {
            return Math.Sqrt(Math.Pow(a, 2) + Math.Pow(b, 2));
        }
    }
}
