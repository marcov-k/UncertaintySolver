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
    static readonly Dictionary<string, OperationType> opTypes = new()
    {
        { "^", OperationType.Exponent },
        { "log", OperationType.Log },
        { "ln", OperationType.Log },
        { "*", OperationType.Multiply },
        { "/", OperationType.Divide },
        { "+", OperationType.Add },
        { "-", OperationType.Subtract }
    };
    static Regex sigfigRegex;
    static Regex nodeRegex;
    static Regex logRegex;
    static Regex parenMultRegex;
    static Regex logMultRegex;
    static Regex parenFixRegex;
    static Regex numRegex;
    static Regex decimalRegex;
    static int minSigfigs, minDecimals = int.MaxValue;
    static bool onlyAdded = true;

    void Start()
    {
        outputField.text = "Result:";

        sigfigRegex = new(@"[1-9]|(?:(?<=(?:[1-9]0*\.[0-9]*)|(?:\.0*[1-9][0-9]*))0)|(?:(?<=[1-9]0*)0(?=0*[1-9\.]))");
        nodeRegex = new(@"^(?<eq>(?<num1>(?:(?<par1>\()?\-?(?(par1).+\)|[0-9.]+(?:\[[0-9.]*\])?))|(?:(?:ln|log)\(.+\)))(?<op>[\^\*\/\+\-])(?<num2>(?:(?<par2>\()?\-?(?(par2).+\)|[0-9.]+(?:\[[0-9.]*\])?))|(?:(?:ln|log)\(.+\))))");
        logRegex = new(@"^(?<op>ln|log)(?<arg>.+\))$");
        parenMultRegex = new(@"(?:(?<=[0-9.])\()|(?:(?<=\))[0-9.])");
        logMultRegex = new(@"(?:(?<=[0-9.])l)");
        parenFixRegex = new(@"(?:(?<!ln|log)\((?=[0-9.\[\]]*(?:(?<!\).*)\))))|(?:^\((?=.*(?:(?<!\).*)\))$))|(?:(?<=(?<!ln|log)\([0-9.\[\]]*)(?<!\).*)\))|(?:(?<=^\(.*)(?<!\).*)\)$)");
        numRegex = new(@"^(?<num>[0-9.]+)(?:\[(?<unc>[0-9.]*)\])?$");
        decimalRegex = new(@"(?<=\.[0-9]*)[0-9]");

        // Test();
    }

    void Test()
    {
        string testInput = "5ln(5)";
        double answer = 5 * Math.Log(5);
        string result = Calculate(testInput);
        Debug.Log($"Test Input: {testInput}, Result: {result}, Correct Answer: {answer}");
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

        Number result;

        if (numRegex.IsMatch(fullEq)) result = new(fullEq);
        else
        {
            Operation treeStart = new(fullEq);
            result = treeStart.Solve();

            if (onlyAdded) result.RoundToDecimals(minDecimals);
            else result.SigFigs = minSigfigs;
        }

        return result.ToScientificString();
    }

    static string FixParentheses(string input)
    {
        if (parenMultRegex.IsMatch(input))
        {
            var multParens = parenMultRegex.Matches(input);

            for (int i = 0; i < multParens.Count; i++)
            {
                input = input.Insert(multParens[i].Index + i, "*");
            }
        }

        if (logMultRegex.IsMatch(input))
        {
            var multLogs = logMultRegex.Matches(input);

            for (int i = 0; i < multLogs.Count; i++)
            {
                input = input.Insert(multLogs[i].Index + i, "*");
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

    public abstract class Node
    {
        public Node[] Children { get; protected set; }

        public virtual Number Solve() => throw new NotImplementedException();

        public Node(string formula) { }
    }

    public class Operation : Node
    {
        OperationType Operator { get; set; }

        public override Number Solve()
        {
            var inputs = new Number[2] { Children[0].Solve(), Children[1].Solve() };

            return Operator switch
            {
                OperationType.Exponent => inputs[0] ^ inputs[1],
                OperationType.Log => Number.Log(inputs[0], inputs[1]),
                OperationType.Multiply => inputs[0] * inputs[1],
                OperationType.Divide => inputs[0] / inputs[1],
                OperationType.Add => inputs[0] + inputs[1],
                OperationType.Subtract => inputs[0] - inputs[1],
                _ => null
            };
        }

        public Operation(string formula) : base(formula)
        {
            formula = FixParentheses(formula);
            var (a, op, b) = FindTopOp(formula);
            Operator = opTypes[op];

            (a, b) = (FixParentheses(a), FixParentheses(b));
            Children = new Node[2] { numRegex.IsMatch(a) ? new Number(a) : new Operation(a), numRegex.IsMatch(b) ? new Number(b) : new Operation(b) };
        }

        static (string a, string op, string b) FindTopOp(string equation)
        {
            if (logRegex.IsMatch(equation))
            {
                var match = logRegex.Match(equation);
                string op = match.Groups["op"].Value;

                string a = op switch
                {
                    "ln" => Math.E.ToString(),
                    "log" => "10",
                    _ => ""
                };

                return (a, op, match.Groups["arg"].Value);
            }
            else
            {
                string subEquation = equation;

                List<IndexedOp> operations = new();
                int charsRemoved = 0;
                while (nodeRegex.IsMatch(subEquation))
                {
                    var match = nodeRegex.Match(subEquation);

                    string num1 = match.Groups["num1"].Value;
                    string op = match.Groups["op"].Value;

                    operations.Add(new(opTypes[op], match.Groups["op"].Index + charsRemoved, op.Length));

                    charsRemoved += num1.Length + op.Length;
                    subEquation = subEquation.Remove(match.Index, num1.Length + op.Length);
                }

                var (topIndex, opLength) = FindTopIndex(operations);

                return (equation.Substring(0, topIndex), equation.Substring(topIndex, opLength), equation.Substring(topIndex + opLength, equation.Length - topIndex - opLength));
            }
        }

        static (int topIndex, int opLength) FindTopIndex(List<IndexedOp> operations)
        {
            for (int i = 0; i < 3; i++)
            {
                List<OperationType> opChecks = new();

                switch (i)
                {
                    case 0:
                        opChecks.AddRange(new List<OperationType>() { opTypes["+"], opTypes["-"] });
                        break;
                    case 1:
                        opChecks.AddRange(new List<OperationType>() { opTypes["*"], opTypes["/"] });
                        break;
                    case 2:
                        opChecks.Add(opTypes["^"]);
                        break;
                }

                foreach (var op in operations)
                {
                    if (opChecks.Contains(op.Op))
                    {
                        return (op.Index, op.OpLength);
                    }
                }
            }

            return (0, 0);
        }

        record IndexedOp(OperationType Op, int Index, int OpLength);
    }

    public class Number : Node
    {
        public double Value
        {
            get => value;
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
            get => sigfigs;
            set
            {
                sigfigs = value;
                UpdateScientificNotation();
            }
        }
        int sigfigs;
        public int Decimals;
        public double Uncertainty { get; private set; }
        public double RelativeUncertainty
        {
            get => Uncertainty / Value;
            set => Uncertainty = value * Value;
        }

        public override string ToString()
        {
            return $"{Value:R}[{Uncertainty}]";
        }

        public static Number operator +(Number a, Number b)
        {
            minSigfigs = Math.Min(minSigfigs, Math.Min(a.SigFigs, b.SigFigs));
            minDecimals = Math.Min(minDecimals, Math.Min(a.Decimals, b.Decimals));
            double value = a.Value + b.Value;
            double uncertainty = AddUncertainty(a.Uncertainty, b.Uncertainty);
            return new(value, uncertainty: uncertainty);
        }

        public static Number operator -(Number a, Number b)
        {
            minSigfigs = Math.Min(minSigfigs, Math.Min(a.SigFigs, b.SigFigs));
            minDecimals = Math.Min(minDecimals, Math.Min(a.Decimals, b.Decimals));
            double value = a.Value - b.Value;
            double uncertainty = AddUncertainty(a.Uncertainty, b.Uncertainty);
            return new(value, uncertainty: uncertainty);
        }

        public static Number operator *(Number a, Number b)
        {
            onlyAdded = false;
            minSigfigs = Math.Min(minSigfigs, Math.Min(a.SigFigs, b.SigFigs));
            minDecimals = Math.Min(minDecimals, Math.Min(a.Decimals, b.Decimals));
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
            onlyAdded = false;
            minSigfigs = Math.Min(minSigfigs, Math.Min(a.SigFigs, b.SigFigs));
            minDecimals = Math.Min(minDecimals, Math.Min(a.Decimals, b.Decimals));
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
            onlyAdded = false;
            minSigfigs = Math.Min(minSigfigs, a.SigFigs);
            minDecimals = Math.Min(minDecimals, a.Decimals);
            double value = Math.Pow(a.Value, b.Value);
            double uncertainty = MathUtils.ExponentUncertainty(a, b);
            Number result = new(value)
            {
                RelativeUncertainty = uncertainty
            };
            return result;
        }

        public static Number Log(Number baseNum, Number arg)
        {
            onlyAdded = false;
            minSigfigs = Math.Min(minSigfigs, arg.SigFigs);
            minDecimals = Math.Min(minDecimals, arg.Decimals);
            double value = Math.Log(arg.Value, baseNum.value);
            double uncertainty = MathUtils.LogUncertainty(baseNum.Value, arg);
            return new(value, uncertainty: uncertainty);
        }

        static double AddUncertainty(double a, double b)
        {
            return useQuadAdd ? MathUtils.QuadratureAdd(a, b) : a + b;
        }

        public override Number Solve() => this;

        public Number(string formula) : base(formula)
        {
            formula = FixParentheses(formula);
            Match numMatch = numRegex.Match(formula);
            Group uncGroup = numMatch.Groups["unc"];
            Uncertainty = string.IsNullOrEmpty(uncGroup.Value) ? 0.0 : double.Parse(uncGroup.Value);
            string valueString = numMatch.Groups["num"].Value;
            value = double.Parse(valueString);
            sigfigs = SigFigCount(valueString);
            Decimals = DecimalCount(valueString);
            ScientificNotation = new(string.Empty);
            UpdateScientificNotation();
        }

        public Number(double value, int? sigfigs = null, double uncertainty = 0) : base("")
        {
            this.value = value;
            this.sigfigs = sigfigs ?? SigFigCount(value.ToString());
            Uncertainty = uncertainty;
            ScientificNotation = new(string.Empty);
            UpdateScientificNotation();
        }

        public string ToScientificString()
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

        string RoundToSigFigs(double value, int sigfigs)
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

        string AddSigFig(string value)
        {
            if (!value.Contains(".")) value += ".";
            value += "0";
            return value;
        }

        string RemoveSigFig(string value)
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

        public static double LogUncertainty(double baseNum, Number arg)
        {
            return arg.RelativeUncertainty / Math.Log(baseNum);
        }

        public static double QuadratureAdd(double a, double b)
        {
            return Math.Sqrt(Math.Pow(a, 2) + Math.Pow(b, 2));
        }
    }

    public enum OperationType
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Exponent,
        Log
    }
}
