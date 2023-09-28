﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace wrapper_tests
{
    [TestClass]
    public class SpeedTests
    {
        [TestMethod]
        public void Given_ZeroVector_ReturnsZero()
        {
            var speedCalc = new SpeedCalculator();
            var speed = speedCalc.CalculateSpeed(0, 0, 1);
            Assert.AreEqual(0, speed);
        }
        
        [TestMethod]
        public void Given_Input_DefaultCalculator_ReturnsUnsmoothed()
        {
            var speedCalc = new SpeedCalculator();
            var inputs = new[]
            {
                (0,0),
                (1,1),
                (2,2),
                (3,3),
            };

            double timePerInput = 1;
            double speed = 0;
            double sum = 0;

            foreach (var input in inputs)
            {
                speed = speedCalc.CalculateSpeed(input.Item1, input.Item2, timePerInput);
                sum += Magnitude(input.Item1, input.Item2);
            }

            double expected = Magnitude(inputs[inputs.Length-1].Item1, inputs[inputs.Length-1].Item2) / timePerInput;

            Assert.AreEqual(expected, speed, 0.0001);
        }

        [TestMethod]
        public void Given_InputForSimpleExponentialSmoothing_SmoothCalculator_Smooths()
        {
            double smoothHalfLife = 50;
            double pollTime = 1;

            var speedArgs = new SpeedCalculatorArgs(
                lp_norm: 2,
                should_smooth: true,
                smooth_halflife: smoothHalfLife,
                use_linear: false);

            var speedCalc = new SpeedCalculator();
            speedCalc.Init(speedArgs);

            var modelSmoother = new SimpleExponentialSmoother(smoothHalfLife);

            var inputs = new[]
            {
                (0,0),
                (1,1),
                (2,2),
                (3,3),
            };

            double actualSpeed = 0;
            double expectedSpeed = 0;

            foreach (var input in inputs)
            {
                actualSpeed = speedCalc.CalculateSpeed(input.Item1, input.Item2, pollTime);

                var magnitude = Magnitude(input.Item1, input.Item2);
                var inputSpeed = magnitude / pollTime;
                expectedSpeed = modelSmoother.Smooth(magnitude, pollTime);
            }

            Assert.AreEqual(expectedSpeed, actualSpeed, 0.0001);
        }

        [TestMethod]
        public void Given_InputForLinearExponentialSmoothing_SmoothCalculator_Smooths()
        {
            double smoothHalflife = 50;
            double pollTime = 1;

            var speedArgs = new SpeedCalculatorArgs(
                lp_norm: 2,
                should_smooth: true,
                smooth_halflife: smoothHalflife,
                use_linear: true);

            var speedCalc = new SpeedCalculator();
            speedCalc.Init(speedArgs);

            var modelSmoother = new LinearExponentialSmoother(smoothHalflife);

            var inputs = new[]
            {
                (0,0),
                (1,1),
                (2,2),
                (3,3),
            };

            double actualSpeed = 0;
            double expectedSpeed = 0;

            foreach (var input in inputs)
            {
                actualSpeed = speedCalc.CalculateSpeed(input.Item1, input.Item2, pollTime);

                var magnitude = Magnitude(input.Item1, input.Item2);
                var inputSpeed = magnitude / pollTime;
                expectedSpeed = modelSmoother.Smooth(magnitude, pollTime);
            }

            Assert.AreEqual(expectedSpeed, actualSpeed, 0.0001);
        }

        public static double Magnitude (double x, double y)
        {
            return Math.Sqrt(x * x + y * y);
        }

        protected interface IMouseSmoother
        {
            double Smooth(double magnitude, double time);
        }

        protected class SimpleExponentialSmoother : IMouseSmoother
        {
            public SimpleExponentialSmoother(double halfLife)
            {
                WindowCoefficient = Math.Pow(0.5, (1 / halfLife));
                CutoffCoefficient = 1 - Math.Sqrt(1 - WindowCoefficient);
                SmoothedSpeeds = new List<double>();
                WindowTotal = 0;
                CutoffTotal = 0;
            }

            public List<double> SmoothedSpeeds { get; }

            protected double WindowCoefficient { get; }

            protected double CutoffCoefficient { get; }

            protected double WindowTotal { get; set; }

            protected double CutoffTotal { get; set; }

            public double Smooth(double speed, double timeDelta)
            {
                var timeAdjustedCoefficient = 1 - Math.Pow(WindowCoefficient, timeDelta);
                WindowTotal += timeAdjustedCoefficient * (speed - WindowTotal);
                var timeAdjustedCutoffCoefficient = 1 - Math.Pow(CutoffCoefficient, timeDelta);
                CutoffTotal += timeAdjustedCutoffCoefficient * (speed - CutoffTotal);

                return Math.Min(WindowTotal, CutoffTotal);
            }
        }

        protected class LinearExponentialSmoother : IMouseSmoother
        {
            public const double TrendHalfLife = 1.25;

            public LinearExponentialSmoother(double halfLife)
            {
                WindowCoefficient = Math.Pow(0.5, 1 / halfLife);
                WindowTrendCoefficient = Math.Pow(0.5, 1 / TrendHalfLife);
                CutoffCoefficient = 1 - Math.Sqrt(1 - WindowCoefficient);
                CutoffTrendCoefficient = 1 - Math.Sqrt(1 - WindowTrendCoefficient);
                SmoothedSpeeds = new List<double>();
                WindowTotal = 0;
                CutoffTotal = 0;
                WindowTrendTotal = 0;
                CutoffTrendTotal = 0;
                TrendDamping = 0.75;
            }

            public List<double> SmoothedSpeeds { get; }

            protected double WindowCoefficient { get; }

            protected double WindowTrendCoefficient { get; }

            protected double CutoffCoefficient { get; }

            protected double CutoffTrendCoefficient { get; }

            protected double WindowTotal { get; set; }

            protected double WindowTrendTotal { get; set; }

            protected double CutoffTotal { get; set; }

            protected double CutoffTrendTotal { get; set; }

            protected double TrendDamping { get; set; }

            public double Smooth(double speed, double timeDelta)
            {
                var oldTotal = WindowTotal;
                var trendEstimate = WindowTrendTotal * timeDelta;
                WindowTotal += TrendDamping * trendEstimate;
                var timeAdjustedWindowCoefficient = 1 - Math.Pow(WindowCoefficient, timeDelta);
                WindowTotal += timeAdjustedWindowCoefficient * (speed - WindowTotal);
                // Don't let a trend carry us below 0
                WindowTotal = Math.Max(WindowTotal, 0);
                var timeAdjustedWindowTrendCoefficient = 1 - Math.Pow(WindowTrendCoefficient, timeDelta);
                WindowTrendTotal *= TrendDamping;
                WindowTrendTotal += timeAdjustedWindowTrendCoefficient * ((WindowTotal - oldTotal) / timeDelta - WindowTrendTotal);

                var oldCutoffTotal = CutoffTotal;
                var cutoffTrendEstimate = CutoffTrendTotal * timeDelta;
                CutoffTotal += TrendDamping * cutoffTrendEstimate;
                var timeAdjustedCutoffCoefficient = 1 - Math.Pow(CutoffCoefficient, timeDelta);
                CutoffTotal += timeAdjustedCutoffCoefficient * (speed - CutoffTotal);
                // Don't let a trend carry us below 0
                CutoffTotal = Math.Max(CutoffTotal, 0);
                var timeAdjustedCutoffTrendCoefficient = 1 - Math.Pow(CutoffTrendCoefficient, timeDelta);
                CutoffTrendTotal *= TrendDamping;
                CutoffTrendTotal += timeAdjustedCutoffTrendCoefficient * ((CutoffTotal - oldCutoffTotal) / timeDelta - CutoffTrendTotal);

                return Math.Min(WindowTotal, CutoffTotal);
            }
        }
    }
}
