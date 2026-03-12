using UnityEngine;
using TMPro;
using System;

public class Calculator : MonoBehaviour
{
    [SerializeField] TMP_InputField userInput;
    [SerializeField] TMP_InputField outputField;

    public void Calculate()
    {

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
