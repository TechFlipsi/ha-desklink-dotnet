// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
#nullable enable
using System;

namespace HaDeskLink.Sendspin;

/// <summary>
/// 1:1 port of the Sendspin time_sync.py two-dimensional Kalman filter
/// (NTP-style clock offset + drift estimation). All constants and the
/// update/transform math match the Python reference exactly.
/// </summary>
public class SendspinTimeFilter
{
    // Residual threshold as multiple of max_error for triggering adaptive forgetting.
    private const double AdaptiveForgettingCutoff = 3.0;

    // Scale factor applied to max_error before it is used as the measurement standard deviation.
    private const double MaxErrorScale = 0.5;

    // SNR threshold for applying drift compensation in time conversions.
    private const double DriftSignificanceThresholdSquared = 2.0 * 2.0;

    private readonly double _processVariance;
    private readonly double _driftProcessVariance;
    private readonly double _forgetVarianceFactor;

    private long _lastUpdate;
    private int _count;

    private double _offset;
    private double _drift;

    private double _offsetCovariance = double.PositiveInfinity;
    private double _offsetDriftCovariance;
    private double _driftCovariance;

    private TimeElement _currentTimeElement = new();

    private readonly object _lock = new();

    private sealed class TimeElement
    {
        public long LastUpdate { get; init; }
        public double Offset { get; init; }
        public double Drift { get; init; }
        public bool UseDrift { get; init; }
    }

    /// <param name="processStdDev">Offset process noise standard deviation (µs per µs).</param>
    /// <param name="forgetFactor">Variance inflation factor for adaptive forgetting.</param>
    /// <param name="driftProcessStdDev">Drift process noise standard deviation.</param>
    public SendspinTimeFilter(double processStdDev = 0.0, double forgetFactor = 2.0,
        double driftProcessStdDev = 1e-11)
    {
        _processVariance = processStdDev * processStdDev;
        _driftProcessVariance = driftProcessStdDev * driftProcessStdDev;
        _forgetVarianceFactor = forgetFactor * forgetFactor;
    }

    /// <summary>
    /// Process a new time synchronization measurement through the Kalman filter.
    /// measurement = ((T2-T1)+(T3-T4))/2, max_error = ((T4-T1)-(T3-T2))/2, both in µs.
    /// </summary>
    public void Update(long measurement, long maxError, long timeAdded)
    {
        lock (_lock)
        {
            if (timeAdded <= _lastUpdate)
            {
                // Skip non-monotonic timestamps to guard against backwards dt in predict
                return;
            }

            double dt = timeAdded - _lastUpdate;
            _lastUpdate = timeAdded;

            double updateStdDev = maxError * MaxErrorScale;
            double measurementVariance = updateStdDev * updateStdDev;

            // First measurement establishes the offset baseline.
            if (_count <= 0)
            {
                _count++;
                _offset = measurement;
                _offsetCovariance = measurementVariance;
                _drift = 0.0; // No drift information available yet
                _currentTimeElement = new TimeElement
                {
                    LastUpdate = _lastUpdate,
                    Offset = _offset,
                    Drift = _drift,
                };
                return;
            }

            // Second measurement: initial drift estimation from finite differences.
            if (_count == 1)
            {
                _count++;
                _drift = (measurement - _offset) / dt;
                _offset = measurement;

                // Drift variance estimated from propagation of offset uncertainties
                _driftCovariance = (_offsetCovariance + measurementVariance) / (dt * dt);
                _offsetCovariance = measurementVariance;

                _currentTimeElement = new TimeElement
                {
                    LastUpdate = _lastUpdate,
                    Offset = _offset,
                    Drift = _drift,
                };
                return;
            }

            // Kalman prediction step: x_k|k-1 = F * x_k-1|k-1
            double offset = _offset + _drift * dt;

            double dtSquared = dt * dt;

            double driftProcessVariance = dt * _driftProcessVariance;
            double newDriftCovariance = _driftCovariance + driftProcessVariance;

            const double offsetDriftProcessVariance = 0.0;
            double newOffsetDriftCovariance =
                _offsetDriftCovariance + _driftCovariance * dt + offsetDriftProcessVariance;

            double offsetProcessVariance = dt * _processVariance;
            double newOffsetCovariance =
                _offsetCovariance + 2 * _offsetDriftCovariance * dt
                + _driftCovariance * dtSquared + offsetProcessVariance;

            // Innovation and adaptive forgetting
            double residual = measurement - offset;
            double maxResidualCutoff = maxError * AdaptiveForgettingCutoff;

            if (_count < 100)
            {
                // Build sufficient history before enabling adaptive forgetting
                _count++;
            }
            else if (Math.Abs(residual) > maxResidualCutoff)
            {
                // Large prediction error - apply forgetting factor to accelerate convergence
                newDriftCovariance *= _forgetVarianceFactor;
                newOffsetDriftCovariance *= _forgetVarianceFactor;
                newOffsetCovariance *= _forgetVarianceFactor;
            }

            // Kalman update step
            double uncertainty = 1.0 / Math.Max(newOffsetCovariance + measurementVariance, 1e-9);

            double offsetGain = newOffsetCovariance * uncertainty;
            double driftGain = newOffsetDriftCovariance * uncertainty;

            _offset = offset + offsetGain * residual;
            _drift += driftGain * residual;

            // Covariance update (simplified form for numerical stability)
            _driftCovariance = newDriftCovariance - driftGain * newOffsetDriftCovariance;
            _offsetDriftCovariance = newOffsetDriftCovariance - driftGain * newOffsetCovariance;
            _offsetCovariance = newOffsetCovariance - offsetGain * newOffsetCovariance;

            // SNR gate: only apply drift when statistically significant
            bool useDrift = _drift * _drift > DriftSignificanceThresholdSquared * _driftCovariance;

            _currentTimeElement = new TimeElement
            {
                LastUpdate = _lastUpdate,
                Offset = _offset,
                Drift = _drift,
                UseDrift = useDrift,
            };
        }
    }

    /// <summary>
    /// Convert a client timestamp to the equivalent server timestamp (µs).
    /// </summary>
    public long ComputeServerTime(long clientTime)
    {
        lock (_lock)
        {
            var element = _currentTimeElement;
            double effectiveDrift = element.UseDrift ? element.Drift : 0.0;

            double dt = clientTime - element.LastUpdate;
            double offset = Math.Round(element.Offset + effectiveDrift * dt);
            return clientTime + (long)offset;
        }
    }

    /// <summary>
    /// Convert a server timestamp to the equivalent client timestamp (µs).
    /// </summary>
    public long ComputeClientTime(long serverTime)
    {
        lock (_lock)
        {
            var element = _currentTimeElement;
            double effectiveDrift = element.UseDrift ? element.Drift : 0.0;

            return (long)Math.Round(
                (serverTime - element.Offset + effectiveDrift * element.LastUpdate)
                / (1.0 + effectiveDrift));
        }
    }

    /// <summary>Reset the filter state.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _count = 0;
            _lastUpdate = 0;
            _offset = 0.0;
            _drift = 0.0;
            _offsetCovariance = double.PositiveInfinity;
            _offsetDriftCovariance = 0.0;
            _driftCovariance = 0.0;
            _currentTimeElement = new TimeElement();
        }
    }

    /// <summary>Number of time sync measurements processed.</summary>
    public int Count
    {
        get { lock (_lock) return _count; }
    }

    /// <summary>True once at least 2 measurements were collected and covariance is finite.</summary>
    public bool IsSynchronized
    {
        get { lock (_lock) return _count >= 2 && !double.IsPositiveInfinity(_offsetCovariance); }
    }

    /// <summary>Standard deviation estimate in microseconds.</summary>
    public long Error
    {
        get { lock (_lock) return (long)Math.Round(Math.Sqrt(_offsetCovariance)); }
    }

    /// <summary>Covariance (variance) estimate for the offset.</summary>
    public long Covariance
    {
        get { lock (_lock) return (long)Math.Round(_offsetCovariance); }
    }

    /// <summary>Current filtered offset estimate in microseconds.</summary>
    public double Offset
    {
        get { lock (_lock) return _offset; }
    }
}