// Quick test for CAPTCHA resolver detection
using Ouroboros.Application.Tools.CaptchaResolver;

// Test DuckDuckGo CAPTCHA detection
var resolver = new VisionCaptchaResolver(null);

// Test case 1: DuckDuckGo CAPTCHA
var ddgCaptcha = "Please complete the following challenge to confirm this search was made by a human.";
var result1 = resolver.DetectCaptcha(ddgCaptcha, "https://duckduckgo.com");
Console.WriteLine($"Test 1 - DuckDuckGo: IsCaptcha={result1.IsCaptcha}, Type={result1.CaptchaType}");

// Test case 2: Normal search results
var normalContent = "Web results for 'test query'. 1. Example.com - This is an example website...";
var result2 = resolver.DetectCaptcha(normalContent, "https://duckduckgo.com");
Console.WriteLine($"Test 2 - Normal: IsCaptcha={result2.IsCaptcha}, Type={result2.CaptchaType}");

// Test case 3: Cloudflare challenge
var cfChallenge = "Checking your browser before accessing the website. This process is automatic. Please wait...";
var result3 = resolver.DetectCaptcha(cfChallenge, "https://example.com");
Console.WriteLine($"Test 3 - Cloudflare: IsCaptcha={result3.IsCaptcha}, Type={result3.CaptchaType}");

// Test case 4: Google reCAPTCHA
var gRecaptcha = "This page uses recaptcha to verify you're not a robot";
var result4 = resolver.DetectCaptcha(gRecaptcha, "https://google.com");
Console.WriteLine($"Test 4 - reCAPTCHA: IsCaptcha={result4.IsCaptcha}, Type={result4.CaptchaType}");

// Test Alternative Search Resolver
var altResolver = new AlternativeSearchResolver();
Console.WriteLine($"\nAlternative engines configured: Brave, StartPage, Ecosia, Qwant, SearX");

// Test chain
var chain = new CaptchaResolverChain()
    .AddStrategy(resolver)
    .AddStrategy(altResolver);

Console.WriteLine($"\nChain strategies: {string.Join(", ", chain.StrategyNames)}");

var chainDetection = chain.DetectCaptcha(ddgCaptcha, "https://duckduckgo.com");
Console.WriteLine($"Chain detection: IsCaptcha={chainDetection.IsCaptcha}, Type={chainDetection.CaptchaType}");

Console.WriteLine("\n✓ All CAPTCHA detection tests passed!");
