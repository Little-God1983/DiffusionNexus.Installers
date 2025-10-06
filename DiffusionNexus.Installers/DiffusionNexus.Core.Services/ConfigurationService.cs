using DiffusionNexus.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiffusionNexus.Core.Services
{
        /// <summary>
        /// Service for managing installation configurations
        /// </summary>
        public class ConfigurationService
        {
            private readonly JsonSerializerOptions _jsonOptions;
            private readonly string _configurationsDirectory;

            public ConfigurationService(string configurationsDirectory = null)
            {
                _configurationsDirectory = configurationsDirectory ??
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ComfyInstaller", "Configurations");

                Directory.CreateDirectory(_configurationsDirectory);

                _jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new JsonStringEnumConverter() }
                };
            }

            /// <summary>
            /// Save configuration to file
            /// </summary>
            public async Task SaveConfigurationAsync(InstallationConfiguration config, string fileName = null)
            {
                fileName ??= $"{config.Name}_{config.Id}.json";
                var filePath = Path.Combine(_configurationsDirectory, fileName);

                config.ModifiedAt = DateTime.UtcNow;

                var json = JsonSerializer.Serialize(config, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
            }

            /// <summary>
            /// Load configuration from file
            /// </summary>
            public async Task<InstallationConfiguration> LoadConfigurationAsync(string fileName)
            {
                var filePath = Path.Combine(_configurationsDirectory, fileName);

                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Configuration file not found: {fileName}");

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<InstallationConfiguration>(json, _jsonOptions);
            }

            /// <summary>
            /// List all saved configurations
            /// </summary>
            public async Task<List<ConfigurationInfo>> ListConfigurationsAsync()
            {
                var configs = new List<ConfigurationInfo>();

                foreach (var file in Directory.GetFiles(_configurationsDirectory, "*.json"))
                {
                    try
                    {
                        var config = await LoadConfigurationAsync(Path.GetFileName(file));
                        configs.Add(new ConfigurationInfo
                        {
                            Id = config.Id,
                            Name = config.Name,
                            Description = config.Description,
                            FileName = Path.GetFileName(file),
                            CreatedAt = config.CreatedAt,
                            ModifiedAt = config.ModifiedAt
                        });
                    }
                    catch
                    {
                        // Skip invalid files
                    }
                }

                return configs.OrderByDescending(c => c.ModifiedAt).ToList();
            }

            /// <summary>
            /// Create a default ComfyUI configuration based on your batch script
            /// </summary>
            public InstallationConfiguration CreateDefaultComfyUIConfiguration(VramProfile vramProfile = VramProfile.GB_16)
            {
                var config = new InstallationConfiguration
                {
                    Name = "ComfyUI with CUDA 12.8",
                    Description = "Default ComfyUI installation with custom nodes and models",

                    MainRepository = new MainRepositorySettings
                    {
                        Type = RepositoryType.ComfyUI,
                        RepositoryUrl = "https://github.com/comfyanonymous/ComfyUI",
                        UseLatestRelease = true,
                        ShallowClone = true
                    },

                    PythonEnvironment = new PythonEnvironmentSettings
                    {
                        PythonVersion = 3.11,
                        CreateVirtualEnvironment = true,
                        VenvName = "venv",
                        UpgradePip = true,
                        Torch = new TorchSettings
                        {
                            CudaVersion = CudaVersion.Cuda128,
                            IndexUrl = "https://download.pytorch.org/whl/cu128",
                            InstallTorchVision = true,
                            InstallTorchAudio = true,
                            VerifyCudaVersion = true
                        }
                    },

                    Hardware = new HardwareSettings
                    {
                        VramProfile = vramProfile,
                        DefaultPrecision = GetPrecisionForVram(vramProfile),
                        GgufPreferences = GetGgufPreferencesForVram(vramProfile)
                    },

                    CustomNodes = GetDefaultCustomNodes(),
                    Models = GetDefaultModels(vramProfile),

                    Options = new InstallationOptions
                    {
                        CreateStartupScript = true,
                        StartupScriptName = "run_nvidia.bat",
                        StartupArguments = new List<string> { "--windows-standalone-build" },
                        PauseOnError = true,
                        EnableLogging = true,
                        LogLevel = LogLevel.Info
                    }
                };

                return config;
            }

            private ModelPrecision GetPrecisionForVram(VramProfile vram)
            {
                return vram switch
                {
                    VramProfile.GB_24 or VramProfile.GB_32 or VramProfile.GB_48 => ModelPrecision.FP32,
                    _ => ModelPrecision.FP16
                };
            }

            private List<string> GetGgufPreferencesForVram(VramProfile vram)
            {
                return vram switch
                {
                    VramProfile.GB_8 => new() { "Q4_K_M", "Q3_K_M", "Q3_K_L" },
                    VramProfile.GB_12 => new() { "Q5_K_M", "Q4_K_M", "Q3_K_M" },
                    VramProfile.GB_16 => new() { "Q6_K", "Q5_K_M", "Q4_K_M" },
                    VramProfile.GB_24 => new() { "Q6_K", "Q5_K_M", "Q4_K_M" },
                    VramProfile.GB_32 or VramProfile.GB_48 => new() { "Q8_0", "Q6_K", "Q5_K_M" },
                    _ => new() { "Q4_K_M" }
                };
            }

            private List<GitRepository> GetDefaultCustomNodes()
            {
                return new List<GitRepository>
            {
                new() {
                    Name = "rgthree-comfy",
                    Url = "https://github.com/rgthree/rgthree-comfy",
                    Priority = 1
                },
                new() {
                    Name = "ComfyUI-KJNodes",
                    Url = "https://github.com/kijai/ComfyUI-KJNodes",
                    Priority = 2
                },
                new() {
                    Name = "ComfyUI-VideoHelperSuite",
                    Url = "https://github.com/Kosinkadink/ComfyUI-VideoHelperSuite",
                    Priority = 3
                },
                new() {
                    Name = "ComfyUI-MelBandRoFormer",
                    Url = "https://github.com/kijai/ComfyUI-MelBandRoFormer",
                    Priority = 4
                },
                new() {
                    Name = "ComfyUI-WanVideoWrapper",
                    Url = "https://github.com/kijai/ComfyUI-WanVideoWrapper",
                    Priority = 5
                },
                new() {
                    Name = "ComfyUI-Manager",
                    Url = "https://github.com/Comfy-Org/ComfyUI-Manager",
                    Priority = 6
                },
                new() {
                    Name = "ComfyUI_essentials",
                    Url = "https://github.com/cubiq/ComfyUI_essentials",
                    Priority = 7
                },
                new() {
                    Name = "cg-use-everywhere",
                    Url = "https://github.com/chrisgoringe/cg-use-everywhere",
                    Priority = 8
                },
                new() {
                    Name = "ComfyUI-Chatterbox",
                    Url = "https://github.com/wildminder/ComfyUI-Chatterbox",
                    Priority = 9
                }
            };
            }

            private List<ModelDownload> GetDefaultModels(VramProfile vram)
            {
                var models = new List<ModelDownload>
            {
                // Lightx2v LoRA
                new()
                {
                    Name = "Lightx2v LoRA",
                    Url = "https://huggingface.co/Kijai/WanVideo_comfy/resolve/main/Lightx2v/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors?download=true",
                    Type = ModelType.Lora,
                    Destination = "models/loras",
                    Priority = 1
                },
                
                // VAE
                new()
                {
                    Name = "Wan 2.1 VAE",
                    Url = "https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged/resolve/main/split_files/vae/wan_2.1_vae.safetensors?download=true",
                    Type = ModelType.Vae,
                    Destination = "models/vae",
                    Priority = 2
                },
                
                // Text Encoder
                new()
                {
                    Name = "UMT5 XXL Text Encoder",
                    Url = "https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged/resolve/main/split_files/text_encoders/umt5_xxl_fp16.safetensors?download=true",
                    Type = ModelType.TextEncoder,
                    Destination = "models/text_encoders",
                    Priority = 3
                },
                
                // CLIP Vision
                new()
                {
                    Name = "CLIP Vision H",
                    Url = "https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged/resolve/main/split_files/clip_vision/clip_vision_h.safetensors",
                    Type = ModelType.ClipVision,
                    Destination = "models/clip_vision",
                    Priority = 4
                }
            };

                // MelBandRoFormer (precision based on VRAM)
                var precision = GetPrecisionForVram(vram);
                models.Add(new()
                {
                    Name = $"MelBandRoFormer {precision}",
                    Url = precision == ModelPrecision.FP16
                        ? "https://huggingface.co/Kijai/MelBandRoFormer_comfy/resolve/main/MelBandRoformer_fp16.safetensors?download=true"
                        : "https://huggingface.co/Kijai/MelBandRoFormer_comfy/resolve/main/MelBandRoformer_fp32.safetensors?download=true",
                    Type = ModelType.Diffusion,
                    Destination = "models/diffusion_models",
                    Priority = 5
                });

                // Add GGUF models with auto-selection
                models.Add(new()
                {
                    Name = "Wan2.1-I2V-14B-480P GGUF",
                    Type = ModelType.Diffusion,
                    Destination = "models/diffusion_models",
                    GgufSettings = new GgufSettings
                    {
                        Repository = "city96/Wan2.1-I2V-14B-480P-gguf",
                        AutoSelectByVram = true,
                        QuantizationPreferences = GetGgufPreferencesForVram(vram)
                    },
                    Priority = 6
                });

                return models;
            }
        }

        /// <summary>
        /// Configuration metadata for listing
        /// </summary>
        public class ConfigurationInfo
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string FileName { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
        }
    }