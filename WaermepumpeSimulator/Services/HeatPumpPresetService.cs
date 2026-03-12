using WaermepumpeSimulator.Models;

namespace WaermepumpeSimulator.Services;

public class HeatPumpPresetService
{
    public List<HeatPumpPreset> GetAllPresets()
    {
        return
        [
            // Panasonic Aquarea L
            new() { Key = "pana_l5", Name = "Aquarea L 5kW (ADC05L6E5AN)", Group = "Panasonic Aquarea L", PMax = "-7, 5.0\n2, 5.0\n7, 5.0", CopData = "35, -7, 3.01\n35, 2, 3.52\n35, 7, 5.05\n55, -7, 2.12\n55, 2, 2.34\n55, 7, 3.07" },
            new() { Key = "pana_l7", Name = "Aquarea L 7kW (ADC07L6E5AN)", Group = "Panasonic Aquarea L", PMax = "-7, 5.8\n2, 6.85\n7, 7.0", CopData = "35, -7, 3.01\n35, 2, 3.43\n35, 7, 4.93\n55, -7, 2.12\n55, 2, 2.34\n55, 7, 2.98" },
            new() { Key = "pana_l9", Name = "Aquarea L 9kW (ADC09L6E5AN)", Group = "Panasonic Aquarea L", PMax = "-7, 7.0\n2, 7.0\n7, 9.0", CopData = "35, -7, 2.8\n35, 2, 3.41\n35, 7, 4.55\n55, -7, 2.13\n55, 2, 2.41\n55, 7, 3.03" },

            // Wolf
            new() { Key = "wolf_cha07", Name = "Wolf CHA-07", Group = "Wolf", PMax = "-7, 6.8\n2, 7.0\n7, 7.0", PMin = "-7, 2.4\n2, 2.2\n7, 2.8", CopData = "35, -7, 2.73\n35, 2, 4.54\n35, 7, 5.47\n35, 10, 5.88\n55, -7, 2.02" },
            new() { Key = "wolf_cha10", Name = "Wolf CHA-10", Group = "Wolf", PMax = "-7, 9.8\n2, 10.0\n7, 10.0", PMin = "-7, 2.3\n2, 2.2\n7, 2.8", CopData = "35, -7, 2.88\n35, 2, 4.65\n35, 7, 5.72\n35, 10, 6.05\n55, -7, 2.06" },

            // Vaillant aroTHERM plus
            new() { Key = "vaillant_35", Name = "aroTHERM plus VWL35/8.1", Group = "Vaillant aroTHERM plus", PMax = "-7, 5.01\n2, 5.52\n7, 6.78", PMin = "-7, 1.89\n2, 1.89\n7, 1.39", CopData = "35, -7, 3.04\n35, 2, 4.07\n35, 7, 4.92\n55, -7, 2.14\n55, 2, 2.57\n55, 7, 2.92" },
            new() { Key = "vaillant_55", Name = "aroTHERM plus VWL55/8.1", Group = "Vaillant aroTHERM plus", PMax = "-7, 5.88\n2, 6.28\n7, 7.2", PMin = "-7, 1.89\n2, 1.89\n7, 1.39", CopData = "35, -7, 2.67\n35, 2, 4.07\n35, 7, 4.92\n55, -7, 2.17\n55, 2, 2.57\n55, 7, 2.92" },
            new() { Key = "vaillant_75", Name = "aroTHERM plus VWL75/8.1", Group = "Vaillant aroTHERM plus", PMax = "-7, 7.25\n2, 8.03\n7, 9.51", PMin = "-7, 2.55\n2, 2.55\n7, 1.93", CopData = "35, -7, 2.67\n35, 2, 4.07\n35, 7, 4.92\n55, -7, 2.13\n55, 2, 2.52\n55, 7, 2.82" },

            // AIRA (IKEA)
            new() { Key = "aira_6", Name = "6 kW HPO-AW-6-230V-1.0", Group = "AIRA (IKEA)", PMax = "-7, 6.0\n2, 7\n7, 9.5", CopData = "35, -7, 2.5\n35, 2, 3.69\n35, 7, 4.52\n35, 12, 6.4\n55, -7, 1.75" },
            new() { Key = "aira_8", Name = "8 kW HPO-AW-8-230V-1.0", Group = "AIRA (IKEA)", PMax = "-7, 8.0\n2, 10\n7, 12", CopData = "35, -7, 2.4\n35, 2, 3.58\n35, 7, 4.73\n35, 12, 6.46\n55, -7, 1.75" },
            new() { Key = "aira_12", Name = "12kW HPO-AW-12-400V-1.0", Group = "AIRA (IKEA)", PMax = "-7, 12.0\n2, 13.0\n7, 16.0", CopData = "35, -7, 2.23\n35, 2, 3.36\n35, 7, 4.94\n35, 12, 6.38\n55, -7, 1.56" },

            // Lambda
            new() { Key = "lambda_10L", Name = "EU10L", Group = "Lambda", PMax = "-7, 8.5\n2, 10.9\n7, 13.7", PMin = "-7, 1.1\n2, 1.7\n7, 2.1", CopData = "35, -7, 3.39\n35, 2, 5.21\n35, 7, 6.02\n55, -7, 2.42\n55, 7, 3.68" },
            new() { Key = "lambda_15L", Name = "EU15L", Group = "Lambda", PMax = "-7, 15.3\n2, 15.7\n7, 20.4", PMin = "-7, 3.9\n2, 4.5\n7, 5.1", CopData = "35, -7, 3.83\n35, 2, 5.11\n35, 7, 5.89\n55, -7, 2.71\n55, 7, 3.47" },
            new() { Key = "lambda_20L", Name = "EU20L", Group = "Lambda", PMax = "-7, 20.8\n2, 25.1\n7, 28.3", PMin = "-7, 4.6\n2, 5.6\n7, 6.7", CopData = "35, -7, 3.7\n35, 2, 5.04\n35, 7, 5.74\n55, -7, 2.62\n55, 7, 3.69" },
            new() { Key = "lambda_35L", Name = "EU35L", Group = "Lambda", PMax = "-7, 34.1\n2, 37.7\n7, 40.0", PMin = "-7, 6.1\n2, 7.0\n7, 8.5", CopData = "35, -7, 3.53\n35, 2, 5.21\n35, 7, 6.01\n55, -7, 2.59\n55, 7, 3.86" },
        ];
    }
}
