using System;
using UnityEngine;

namespace CyberRider.Unity
{
    /// <summary>
    /// Procedural soundtrack and effects: a pulsing bass beat while riding, a soft pad while editing,
    /// and short synth cues for tricks, crashes and finishes. Every clip is generated at startup.
    /// </summary>
    public sealed class AudioSynth : MonoBehaviour, IGameAudio
    {
        private AudioSource _sfx;
        private AudioSource _pad;
        private AudioClip _kick, _kick2, _hat, _hatLoud, _bass, _bass2, _trickSmall, _trickMid, _trickBig, _pickup, _crash, _finish, _boom;
        private double _nextBeat;
        private int _beatIndex;
        private bool _enabled = true;
        private const double Bpm = 96;
        private int _rate = 44100;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (_pad != null) _pad.volume = value ? 0.12f : 0f;
                if (_sfx != null) _sfx.volume = value ? 0.6f : 0f;
            }
        }

        public bool Riding { get; set; }

        private void Awake()
        {
            _rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _pad = gameObject.AddComponent<AudioSource>();
            _pad.playOnAwake = false;
            _pad.loop = true;
            _kick = Kick("kick", 120, 38, 0.3);
            _kick2 = Kick("kick2", 100, 38, 0.3);
            _hat = Noise("hat", 0.05, 0.06, true);
            _hatLoud = Noise("hat2", 0.05, 0.12, true);
            _bass = Pluck("bass", 55, 0.35, 0.18);
            _bass2 = Pluck("bass2", 65.41, 0.35, 0.18);
            _trickSmall = Arp("trick1", 523.25, 2, 0.06, 0.18);
            _trickMid = Arp("trick2", 523.25, 3, 0.06, 0.18);
            _trickBig = Arp("trick3", 440, 4, 0.06, 0.18);
            _pickup = Tones("pickup", new[] { 880.0, 1318.5 }, new[] { 0.0, 0.08 }, 0.16, 0.12);
            _crash = Noise("crash", 0.35, 0.35, false);
            _finish = Tones("finish", new[] { 523.25, 659.25, 783.99, 1046.5 }, new[] { 0.0, 0.09, 0.18, 0.27 }, 0.35, 0.1);
            _boom = Boom("boom");
            _pad.clip = Pad("pad");
            _pad.volume = 0.12f;
            _pad.Play();
            _nextBeat = AudioSettings.dspTime + 0.1;
        }

        private void Update()
        {
            double period = 60.0 / Bpm / 2;
            double now = AudioSettings.dspTime;
            while (_nextBeat < now + 0.02)
            {
                if (Riding && _enabled) PlayBeat(_beatIndex);
                _nextBeat += period;
                _beatIndex = (_beatIndex + 1) % 8;
            }
        }

        private void PlayBeat(int i)
        {
            if (i % 2 == 0) _sfx.PlayOneShot(i % 4 == 0 ? _kick : _kick2, 0.9f);
            else _sfx.PlayOneShot(i == 7 ? _hatLoud : _hat, 1f);
            if (i == 0) _sfx.PlayOneShot(_bass, 0.7f);
            else if (i == 6) _sfx.PlayOneShot(_bass2, 0.7f);
        }

        public void Trick(int points)
        {
            if (!_enabled) return;
            _sfx.PlayOneShot(points >= 500 ? _trickBig : points >= 150 ? _trickMid : _trickSmall, 0.5f);
        }

        public void Pickup()
        {
            if (_enabled) _sfx.PlayOneShot(_pickup, 0.6f);
        }

        public void Crash()
        {
            if (_enabled) _sfx.PlayOneShot(_crash, 0.8f);
        }

        public void Finish()
        {
            if (_enabled) _sfx.PlayOneShot(_finish, 0.6f);
        }

        public void Explosion()
        {
            if (_enabled) _sfx.PlayOneShot(_boom, 0.9f);
        }

        // ------------------------------------------------------------------ generators

        private AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, _rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip Kick(string name, double f0, double f1, double dur)
        {
            int n = (int)(_rate * dur);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)_rate;
                double f = f0 * Math.Pow(f1 / f0, Math.Min(1, t / 0.16));
                phase += f / _rate;
                double env = Math.Exp(-t * 12);
                d[i] = (float)(Math.Sin(phase * Math.PI * 2) * env * 0.9);
            }
            return Make(name, d);
        }

        private AudioClip Noise(string name, double dur, double vol, bool highpass)
        {
            int n = (int)(_rate * dur);
            var d = new float[n];
            var rng = new System.Random(7);
            double prev = 0;
            double prevIn = 0;
            for (int i = 0; i < n; i++)
            {
                double x = rng.NextDouble() * 2 - 1;
                double env = highpass ? 1 - i / (double)n : Math.Pow(1 - i / (double)n, 2);
                double y;
                if (highpass)
                {
                    // Simple first-order high-pass keeps the hiss, drops the thump.
                    y = 0.9 * (prev + x - prevIn);
                    prevIn = x;
                    prev = y;
                }
                else
                {
                    // Low-pass rumble for crashes.
                    y = prev + 0.15 * (x - prev);
                    prev = y;
                }
                d[i] = (float)(y * env * vol * (highpass ? 1.5 : 3.5));
            }
            return Make(name, d);
        }

        private AudioClip Pluck(string name, double freq, double dur, double vol)
        {
            int n = (int)(_rate * dur);
            var d = new float[n];
            double phase = 0;
            double lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)_rate;
                phase += freq / _rate;
                double sq = (phase % 1) < 0.5 ? 1 : -1;
                double cutoff = 0.05 + 0.4 * Math.Exp(-t * 8);
                lp += cutoff * (sq - lp);
                double env = Math.Exp(-t * 9);
                d[i] = (float)(lp * env * vol * 2);
            }
            return Make(name, d);
        }

        private AudioClip Arp(string name, double baseFreq, int notes, double gap, double dur)
        {
            var freqs = new double[notes];
            var starts = new double[notes];
            for (int i = 0; i < notes; i++)
            {
                freqs[i] = baseFreq * Math.Pow(2, i / 12.0 * 4);
                starts[i] = i * gap;
            }
            return Tones(name, freqs, starts, dur, 0.08, true);
        }

        private AudioClip Tones(string name, double[] freqs, double[] starts, double dur, double vol, bool square = false)
        {
            double total = 0;
            for (int i = 0; i < freqs.Length; i++) total = Math.Max(total, starts[i] + dur + 0.02);
            int n = (int)(_rate * total);
            var d = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                int s0 = (int)(starts[k] * _rate);
                int len = (int)(dur * _rate);
                double phase = 0;
                for (int i = 0; i < len && s0 + i < n; i++)
                {
                    double t = i / (double)_rate;
                    phase += freqs[k] / _rate;
                    double wave = square ? ((phase % 1) < 0.5 ? 1 : -1) * 0.5 : Math.Sin(phase * Math.PI * 2);
                    double env = Math.Min(1, t / 0.01) * Math.Exp(-t * (3 / dur));
                    d[s0 + i] += (float)(wave * env * vol);
                }
            }
            return Make(name, d);
        }

        private AudioClip Boom(string name)
        {
            int n = (int)(_rate * 0.6);
            var d = new float[n];
            var rng = new System.Random(3);
            double prev = 0;
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)_rate;
                double x = rng.NextDouble() * 2 - 1;
                prev += 0.08 * (x - prev);
                phase += 55.0 / _rate;
                double env = Math.Exp(-t * 5);
                d[i] = (float)((prev * 2.5 + Math.Sin(phase * Math.PI * 2) * 0.5) * env * 0.8);
            }
            return Make(name, d);
        }

        private AudioClip Pad(string name)
        {
            double dur = 4.0;
            int n = (int)(_rate * dur);
            var d = new float[n];
            double[] freqs = { 55, 82.41, 110, 164.81 };
            var phases = new double[freqs.Length];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)_rate;
                double sum = 0;
                for (int k = 0; k < freqs.Length; k++)
                {
                    double detune = 1 + Math.Sin(t * (0.07 + k * 0.03) * Math.PI * 2) * 0.0004;
                    phases[k] += freqs[k] * detune / _rate;
                    double p = phases[k] % 1;
                    // Triangle waves stay soft without a filter.
                    double tri = 4 * Math.Abs(p - 0.5) - 1;
                    sum += tri * (k % 2 == 0 ? 0.35 : 0.25);
                }
                double swell = 0.7 + 0.3 * Math.Sin(t / dur * Math.PI * 2);
                // Short crossfade at the loop point.
                double fade = Math.Min(1, Math.Min(t, dur - t) / 0.05);
                d[i] = (float)(sum * 0.25 * swell * fade);
            }
            return Make(name, d);
        }
    }
}
