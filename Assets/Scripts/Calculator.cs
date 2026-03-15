using UnityEngine;
using TMPro;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public class Calculator : MonoBehaviour
{
    [SerializeField] TMP_InputField userInput;
    [SerializeField] TMP_InputField outputField;
    readonly List<string> opOrder = new() { "(", "^", "*", "/", "+", "-" };
    Regex sigfigRegex;
    Regex equationRegex;
    Regex parenMultRegex;
    Regex parenFixRegex;

    void Start()
    {
        sigfigRegex = new(@"[1-9]|(?:(?<=(?:[1-9]0*\.[0-9]*)|(?:\.0*[1-9][0-9]*))0)|(?:(?<=[1-9]0*)0(?=0*[1-9\.]))");
        equationRegex = new(@"^(?<eq>(?<num1>(?<par1>\()?\-?(?(par1).+\)|[0-9.]+))(?<op>[\^\*\/\+\-])(?<num2>(?<par2>\()?\-?(?(par2).+\)|[0-9.]+)))");
        parenMultRegex = new(@"(?:(?<=[0-9.])\()|(?:(?<=\))[0-9.])");
        parenFixRegex = new(@"(?:\((?=[0-9.]*\)))|(?:^\((?=.*\)$))|(?:(?<=\([0-9.]*)\))|(?:(?<=^\(.*)\)$)");

        string testInput = "((9*5)/(10+5x5))5";
        double testOutput = ((9.0 * 5.0) / (10.0 + (5.0 * 5.0))) * 5.0;
        Debug.Log($"Test input: {testInput}, test result: {Calculate(testInput)}, correct result: {testOutput}");
    }

    public string Calculate(string input)
    {
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

        return fullEq;
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

        double num1 = double.Parse(match.Groups["num1"].Value);
        string op = match.Groups["op"].Value;
        double num2 = double.Parse(match.Groups["num2"].Value);

        var result = op switch
        {
            "^" => Math.Pow(num1, num2),
            "*" => num1 * num2,
            "/" => num1 / num2,
            "+" => num1 + num2,
            "-" => num1 - num2,
            _ => 0,
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

    int SigFigCount(string input)
    {
        var matches = sigfigRegex.Matches(input);
        return matches.Count;
    }

    public struct Number
    {
        public double Value { get; private set; }
        public double Uncertainty { get; private set; }
        public double RelativeUncertainty
        {
            readonly get => Uncertainty / Value;
            set => Uncertainty = value * Value;
        }

        public static Number operator +(Number a, Number b)
        {
            double value = a.Value + b.Value;
            double uncertainty = MathUtils.QuadratureAdd(a.Uncertainty, b.Uncertainty);
            return new(value, uncertainty);
        }

        public static Number operator -(Number a, Number b)
        {
            double value = a.Value - b.Value;
            double uncertainty = MathUtils.QuadratureAdd(a.Uncertainty, b.Uncertainty);
            return new(value, uncertainty);
        }

        public static Number operator *(Number a, Number b)
        {
            double value = a.Value * b.Value;
            double uncertainty = MathUtils.QuadratureAdd(a.RelativeUncertainty, b.RelativeUncertainty);
            Number result = new(value)
            {
                RelativeUncertainty = uncertainty
            };
            return result;
        }

        public static Number operator /(Number a, Number b)
        {
            double value = a.Value / b.Value;
            double uncertainty = MathUtils.QuadratureAdd(a.RelativeUncertainty, b.RelativeUncertainty);
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

        public Number(double value, double uncertainty = 0)
        {
            Value = value;
            Uncertainty = uncertainty;
        }
    }

    public static class MathUtils
    {
        public static double ExponentUncertainty(Number a, Number b)
        {
            double uncertA = b.Value * a.RelativeUncertainty;
            double uncertB = Math.Log(a.Value) * b.Uncertainty;
            return QuadratureAdd(uncertA, uncertB);
        }

        public static double QuadratureAdd(double a, double b)
        {
            return Math.Sqrt(Math.Pow(a, 2) + Math.Pow(b, 2));
        }
    }
}
