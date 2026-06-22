using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Artemis
{
    /// <summary>軌道の1サンプル（§8 CSVの1行）。位置・速度はkm/(km/s)をdoubleで保持。</summary>
    public struct TrajectorySample
    {
        public double tSec;
        public string phase;            // parking / TLI / outbound / flyby / return
        public double ox, oy, oz;       // Orion位置 [km]
        public double mx, my, mz;       // 月位置 [km]
        public double ovx, ovy, ovz;    // Orion速度 [km/s]
    }

    /// <summary>
    /// §8 仕様の CSV を読み込む。Unityの32bit floatに頼らず double で解析し、
    /// 表示直前にのみ float へ変換する（§9.1 精度方針）。
    /// </summary>
    public static class TrajectoryLoader
    {
        public static List<TrajectorySample> Parse(string csvText)
        {
            var list = new List<TrajectorySample>();
            var ci = CultureInfo.InvariantCulture;
            var lines = csvText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim().TrimEnd('\r');
                if (i == 0 && line.StartsWith("t_sec")) continue; // ヘッダ
                if (string.IsNullOrWhiteSpace(line)) continue;
                var c = line.Split(',');
                if (c.Length < 11) continue;
                double D(int k) => double.Parse(c[k], ci);
                list.Add(new TrajectorySample
                {
                    tSec = D(0),
                    phase = c[1].Trim(),
                    ox = D(2), oy = D(3), oz = D(4),
                    mx = D(5), my = D(6), mz = D(7),
                    ovx = D(8), ovy = D(9), ovz = D(10),
                });
            }
            return list;
        }

        /// <summary>線形補間で任意時刻のサンプルを得る（再生の可変速・滑らか化に使用）。</summary>
        public static TrajectorySample Interpolate(List<TrajectorySample> d, double t)
        {
            if (d.Count == 0) return default;
            if (t <= d[0].tSec) return d[0];
            if (t >= d[d.Count - 1].tSec) return d[d.Count - 1];
            int lo = 0, hi = d.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (d[mid].tSec <= t) lo = mid; else hi = mid;
            }
            var a = d[lo]; var b = d[hi];
            double f = (t - a.tSec) / (b.tSec - a.tSec);
            return new TrajectorySample
            {
                tSec = t,
                phase = f < 0.5 ? a.phase : b.phase,
                ox = a.ox + (b.ox - a.ox) * f, oy = a.oy + (b.oy - a.oy) * f, oz = a.oz + (b.oz - a.oz) * f,
                mx = a.mx + (b.mx - a.mx) * f, my = a.my + (b.my - a.my) * f, mz = a.mz + (b.mz - a.mz) * f,
                ovx = a.ovx + (b.ovx - a.ovx) * f, ovy = a.ovy + (b.ovy - a.ovy) * f, ovz = a.ovz + (b.ovz - a.ovz) * f,
            };
        }
    }
}
