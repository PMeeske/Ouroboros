# Android App Testing - Official Frameworks and Best Practices

## Executive Summary

After investigating official testing frameworks from Microsoft and Google/Android, here are the recommended approaches for testing the .NET MAUI Android app:

### Official Microsoft Solutions

1. **Xamarin.UITest** - Microsoft's official UI testing framework
2. **Appium with .NET** - Cross-platform mobile automation (Microsoft-supported)
3. **Unit Testing with xUnit/NUnit** - For business logic and view models

### Official Google/Android Solutions

1. **Espresso** - Google's official Android UI testing framework (Java/Kotlin only)
2. **UI Automator** - For cross-app UI testing (Java/Kotlin only)
3. **Robolectric** - Unit tests that run on JVM (Java/Kotlin only)

### Recommendation for .NET MAUI Android

Since this is a **.NET MAUI app written in C#**, the native Android testing frameworks (Espresso, UI Automator) **cannot be used directly**. Instead, we should use:

1. **Xamarin.UITest** - Microsoft's official choice for MAUI/Xamarin apps
2. **Appium with C# bindings** - Industry standard cross-platform testing
3. **Custom test harness** (like our MauiApplicationFactory) - For integration tests

---

## 1. Xamarin.UITest (Microsoft Official)

### Overview
Microsoft's official UI testing framework for Xamarin and .NET MAUI applications. It allows writing tests in C# that interact with the app UI.

### Installation

```xml
<PackageReference Include="Xamarin.UITest" Version="4.3.4" />
<PackageReference Include="NUnit" Version="4.2.2" />
```

### Example Test

```csharp
using NUnit.Framework;
using Xamarin.UITest;
using Xamarin.UITest.Android;

namespace Ouroboros.Android.UITests
{
    [TestFixture(Platform.Android)]
    public class MainPageTests
    {
        private IApp app;
        private readonly Platform platform;

        public MainPageTests(Platform platform)
        {
            this.platform = platform;
        }

        [SetUp]
        public void BeforeEachTest()
        {
            // Initialize the app
            app = ConfigureApp
                .Android
                .ApkFile("path/to/your/app.apk")
                .StartApp();
        }

        [Test]
        public void AppLaunches_ShouldDisplayWelcomeScreen()
        {
            // Arrange & Act
            var welcomeMessage = app.Query(c => c.Marked("OutputLabel")).FirstOrDefault();

            // Assert
            Assert.IsNotNull(welcomeMessage);
            Assert.That(welcomeMessage.Text, Does.Contain("Ouroboros CLI"));
        }

        [Test]
        public void PurpleScreenBugFix_DatabaseError_ShouldStillShowUI()
        {
            // Arrange - App launches with potential database error
            app.WaitForElement(c => c.Marked("OutputLabel"), timeout: TimeSpan.FromSeconds(10));

            // Act
            var outputLabel = app.Query(c => c.Marked("OutputLabel")).FirstOrDefault();

            // Assert - UI must be visible (not purple screen)
            Assert.IsNotNull(outputLabel, "Output label must be visible");
            var text = outputLabel.Text;
            
            // Either shows successful init or error message (both are OK, purple screen is NOT OK)
            Assert.That(text, Does.Contain("Ouroboros CLI").Or.Contains("⚠ Initialization error"));
            Assert.That(text, Does.Contain("> "), "Terminal prompt must be visible");
        }

        [Test]
        public void ExecuteCommand_TypeHelpAndClickExecute_ShouldShowResponse()
        {
            // Arrange
            app.WaitForElement(c => c.Marked("CommandEntry"));

            // Act
            app.Tap(c => c.Marked("CommandEntry"));
            app.EnterText("help");
            app.Tap(c => c.Marked("ExecuteButton"));

            // Assert
            app.WaitForElement(c => c.Text("help"), timeout: TimeSpan.FromSeconds(5));
            var output = app.Query(c => c.Marked("OutputLabel")).FirstOrDefault();
            Assert.That(output.Text, Does.Contain("help"));
        }

        [Test]
        public void QuickCommandButton_ClickHelp_ShouldExecuteImmediately()
        {
            // Arrange
            app.WaitForElement(c => c.Text("help"));

            // Act
            app.Tap(c => c.Text("help"));

            // Assert
            app.WaitForElement(c => c.Text("Available commands"), timeout: TimeSpan.FromSeconds(5));
        }

        [TearDown]
        public void AfterEachTest()
        {
            // Optional: Screenshot on failure
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                app.Screenshot($"Failed-{TestContext.CurrentContext.Test.Name}");
            }
        }
    }
}
```

### Setup Requirements

1. **Android SDK** must be installed
2. **APK file** of the app
3. **Android Emulator** or physical device
4. **Java Development Kit (JDK)**

### Configuration

```csharp
// In a config file or setup method
public static IApp ConfigureApp
{
    get
    {
        return ConfigureApp
            .Android
            .ApkFile("../../../com.adaptivesystems.Ouroboros.apk")
            .DeviceSerial("emulator-5554") // Optional: specific device
            .StartApp();
    }
}
```

---

## 2. Appium with .NET (Cross-Platform Standard)

### Overview
Appium is the industry-standard cross-platform mobile automation framework. It works with iOS, Android, and Windows apps.

### Installation

```xml
<PackageReference Include="Appium.WebDriver" Version="5.0.0-rc.1" />
<PackageReference Include="Selenium.WebDriver" Version="4.16.2" />
<PackageReference Include="xunit" Version="2.9.3" />
```

### Example Test

```csharp
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using Xunit;

namespace Ouroboros.Android.AppiumTests
{
    public class MainPageAppiumTests : IDisposable
    {
        private readonly AndroidDriver driver;

        public MainPageAppiumTests()
        {
            var appiumOptions = new AppiumOptions();
            appiumOptions.AddAdditionalAppiumOption("platformName", "Android");
            appiumOptions.AddAdditionalAppiumOption("deviceName", "Android Emulator");
            appiumOptions.AddAdditionalAppiumOption("app", "/path/to/app.apk");
            appiumOptions.AddAdditionalAppiumOption("automationName", "UiAutomator2");

            driver = new AndroidDriver(
                new Uri("http://localhost:4723"),
                appiumOptions,
                TimeSpan.FromSeconds(180)
            );
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
        }

        [Fact]
        public void AppLaunches_NoP urpleScreen_UIIsVisible()
        {
            // Arrange & Act
            var outputLabel = driver.FindElement(MobileBy.AccessibilityId("OutputLabel"));

            // Assert
            Assert.NotNull(outputLabel);
            Assert.Contains("Ouroboros CLI", outputLabel.Text);
        }

        [Fact]
        public void ExecuteCommand_EnterTextAndTapExecute_ShowsResult()
        {
            // Arrange
            var commandEntry = driver.FindElement(MobileBy.AccessibilityId("CommandEntry"));
            var executeButton = driver.FindElement(MobileBy.AccessibilityId("ExecuteButton"));

            // Act
            commandEntry.SendKeys("help");
            executeButton.Click();

            // Assert
            var outputLabel = driver.FindElement(MobileBy.AccessibilityId("OutputLabel"));
            Assert.Contains("help", outputLabel.Text);
        }

        public void Dispose()
        {
            driver?.Quit();
        }
    }
}
```

### Setup Requirements

1. **Appium Server** running (install via npm: `npm install -g appium`)
2. **Android SDK** and **emulator** or physical device
3. **UiAutomator2 driver**: `appium driver install uiautomator2`

### Start Appium Server

```bash
appium --base-path /wd/hub --port 4723
```

---

## 3. Comparison: Xamarin.UITest vs Appium

| Feature | Xamarin.UITest | Appium |
|---------|---------------|---------|
| **Vendor** | Microsoft | Open Source (Appium Project) |
| **Language** | C# only | Multiple (C#, Java, Python, etc.) |
| **Platform Support** | Android, iOS | Android, iOS, Windows, Web |
| **Learning Curve** | Easy (C# native) | Moderate (Selenium-based) |
| **MAUI Support** | Official | Community |
| **REPL** | Yes (excellent for exploration) | No |
| **Cost** | Free | Free |
| **CI/CD Integration** | Excellent | Excellent |
| **Community** | Microsoft/Xamarin | Very Large |

### Recommendation
- **Use Xamarin.UITest** if:
  - You're primarily testing .NET MAUI/Xamarin apps
  - You want official Microsoft support
  - You prefer C#-native testing
  - You need the REPL for test development

- **Use Appium** if:
  - You need cross-platform testing (iOS + Android + Windows)
  - You want industry-standard tooling
  - You're already using Appium for other apps
  - You need multi-language support

---

## 4. Updated Test Project Structure

Let me update the Android test project to use the official frameworks:

### Updated .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <!-- Official Microsoft UI Testing -->
    <PackageReference Include="Xamarin.UITest" Version="4.3.4" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
    
    <!-- Alternative: Appium for cross-platform testing -->
    <PackageReference Include="Appium.WebDriver" Version="5.0.0-rc.1" />
    <PackageReference Include="Selenium.WebDriver" Version="4.16.2" />
    
    <!-- Unit testing frameworks -->
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="8.7.1" />
    <PackageReference Include="Moq" Version="4.20.72" />
  </ItemGroup>

</Project>
```

---

## 5. Integration with CI/CD

### GitHub Actions Example

```yaml
name: Android UI Tests

on:
  pull_request:
    branches: [ main ]
  push:
    branches: [ main ]

jobs:
  ui-tests:
    runs-on: macos-latest  # macOS has better Android emulator support
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Setup Java
      uses: actions/setup-java@v4
      with:
        distribution: 'microsoft'
        java-version: '17'
    
    - name: Install Android SDK
      uses: android-actions/setup-android@v3
    
    - name: Build APK
      run: |
        cd src/Ouroboros.Android
        dotnet build -c Release -f net8.0-android
    
    - name: Start Android Emulator
      uses: reactivecircus/android-emulator-runner@v2
      with:
        api-level: 29
        target: default
        arch: x86_64
        profile: Nexus 6
        script: echo "Emulator started"
    
    - name: Run Xamarin.UITest
      run: |
        cd src/Ouroboros.Android.UITests
        dotnet test --logger "trx;LogFileName=test-results.trx"
    
    - name: Upload Test Results
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: test-results
        path: '**/test-results.trx'
```

---

## 6. Best Practices for .NET MAUI Android Testing

### Accessibility IDs
Always set AutomationId for testable elements:

```xml
<!-- MainPage.xaml -->
<Label x:Name="OutputLabel"
       AutomationId="OutputLabel"
       Text="{Binding Output}" />

<Entry x:Name="CommandEntry"
       AutomationId="CommandEntry"
       Placeholder="Enter command..." />

<Button x:Name="ExecuteButton"
        AutomationId="ExecuteButton"
        Text="Execute"
        Clicked="OnExecuteClicked" />
```

### Page Object Pattern

```csharp
public class MainPageObject
{
    private readonly IApp app;

    public MainPageObject(IApp app)
    {
        this.app = app;
    }

    public string Output => app.Query(c => c.Marked("OutputLabel")).FirstOrDefault()?.Text ?? "";

    public void TypeCommand(string command)
    {
        app.Tap(c => c.Marked("CommandEntry"));
        app.ClearText();
        app.EnterText(command);
    }

    public void ClickExecute()
    {
        app.Tap(c => c.Marked("ExecuteButton"));
    }

    public void ExecuteCommand(string command)
    {
        TypeCommand(command);
        ClickExecute();
    }

    public bool IsLoaded()
    {
        return app.Query(c => c.Marked("OutputLabel")).Any();
    }

    public void WaitForOutput(string expectedText, int timeoutSeconds = 10)
    {
        app.WaitForElement(c => c.Text(expectedText), timeout: TimeSpan.FromSeconds(timeoutSeconds));
    }
}
```

---

## 7. Summary and Recommendations

### For the Ouroboros Android App:

1. **Primary Testing Approach**: Use our custom `MauiApplicationFactory` for fast integration tests
2. **UI Testing**: Use Xamarin.UITest for comprehensive UI automation tests
3. **CI/CD**: Run UI tests in GitHub Actions with Android emulator

### Test Strategy:
- **Unit Tests** (xUnit): Business logic, view models, services (fastest, run on every commit)
- **Integration Tests** (MauiApplicationFactory): MainPage initialization, error handling (fast, run on every commit)
- **UI Tests** (Xamarin.UITest): End-to-end user workflows (slower, run on PRs and releases)

### Implementation Priority:
1. ✅ Unit tests for services (already have)
2. ✅ Integration tests with MauiApplicationFactory (already created)
3. ⏭️ Add Xamarin.UITest for actual UI automation
4. ⏭️ Set up CI/CD with Android emulator

This layered approach provides:
- **Fast feedback** from unit and integration tests
- **Comprehensive coverage** from UI tests
- **Confidence** that the purple screen bug is fixed and won't return
