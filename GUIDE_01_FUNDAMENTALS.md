# Comprehensive Guide Part 1: C# Fundamentals & Core Concepts

## Table of Contents
1. [Sealed Classes](#sealed-classes)
2. [Static Classes](#static-classes)
3. [Required Keyword](#required-keyword)
4. [Init vs Set](#init-vs-set)
5. [Streams](#streams)
6. [CancellationToken](#cancellationtoken)
7. [Buffers](#buffers)
8. [Async/Await](#asyncawait)

---

## 1. Sealed Classes

### What is `sealed`?
```csharp
public sealed class AppOptions { }
```

**Definition**: A `sealed` class **cannot be inherited** by other classes.

### Why Use Sealed?

#### ✅ **Performance**
- Compiler can optimize sealed classes better
- Method calls can be devirtualized (no virtual dispatch)

#### ✅ **Security & Design Intent**
- Prevents unintended inheritance
- Ensures class behavior cannot be modified

#### ✅ **Immutability Guarantee**
- Combined with `init` properties, ensures thread-safety

### Example:
```csharp
// ✅ This works
public sealed class AppOptions 
{
    public required string Source { get; init; }
}

// ❌ This fails - cannot inherit from sealed class
public class ExtendedOptions : AppOptions  // Compiler error!
{
}
```

### When to Use Sealed?
- ✅ DTOs (Data Transfer Objects)
- ✅ Configuration classes
- ✅ Value objects
- ✅ Classes not designed for inheritance

---

## 2. Static Classes

### What is `static`?
```csharp
public static class LoggerFactory { }
```

**Definition**: A `static` class:
- **Cannot be instantiated** (no `new LoggerFactory()`)
- **Cannot be inherited**
- Can only contain **static members**

### Why Use Static?

#### ✅ **Utility/Helper Methods**
```csharp
public static class LoggerFactory
{
    public static ILogger Create()  // Factory method
    {
        return new LoggerConfiguration().CreateLogger();
    }
}

// Usage
var logger = LoggerFactory.Create();  // No 'new' keyword
```

#### ✅ **Extension Methods**
```csharp
public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string value)
    {
        return string.IsNullOrEmpty(value);
    }
}
```

### Static vs Instance:
```csharp
// ❌ Static class - cannot instantiate
public static class MathHelper
{
    public static int Add(int a, int b) => a + b;
}
var result = MathHelper.Add(5, 3);  // ✅ Direct call

// ✅ Instance class - must instantiate
public class Calculator
{
    public int Add(int a, int b) => a + b;
}
var calc = new Calculator();  // Must create instance
var result = calc.Add(5, 3);
```

---

## 3. Required Keyword

### What is `required`?
```csharp
public required string Source { get; init; }
```

**Definition**: Forces the property to be set during object initialization.

### Before C# 11 (Without `required`):
```csharp
public class AppOptions
{
    public string Source { get; init; }  // Can be null!
}

var options = new AppOptions();  // ✅ Compiles, but Source is null!
```

### After C# 11 (With `required`):
```csharp
public class AppOptions
{
    public required string Source { get; init; }
}

var options = new AppOptions();  // ❌ Compiler error!
var options = new AppOptions { Source = "test" };  // ✅ Works
```

### Why Use Required?
- ✅ **Compile-time safety** - prevents null reference exceptions
- ✅ **Clear intent** - documents mandatory properties
- ✅ **Better than constructors** for many properties

---

## 4. Init vs Set

### `set` (Mutable)
```csharp
public string Source { get; set; }
```
- Can be changed **anytime** after creation
- **Not thread-safe**

```csharp
var settings = new AppSettings { Source = "toom" };
settings.Source = "changed";  // ✅ Allowed
```

### `init` (Immutable)
```csharp
public string Source { get; init; }
```
- Can **only** be set during initialization
- **Thread-safe** (cannot change)

```csharp
var options = new AppOptions { Source = "toom" };
options.Source = "changed";  // ❌ Compiler error!
```

### When to Use Each?

| Use Case | Use `set` | Use `init` |
|----------|-----------|------------|
| Configuration DTOs | ✅ | ❌ |
| Runtime options | ❌ | ✅ |
| Thread-safe objects | ❌ | ✅ |
| Mutable state | ✅ | ❌ |

---

## 5. Streams

### What is a Stream?
**Definition**: A `Stream` is an **abstract representation** of a sequence of bytes.

Think of it like a **pipe** where data flows through.

### Types of Streams:
```csharp
// File stream - reads/writes files
FileStream fileStream = File.OpenRead("file.pdf");

// Memory stream - reads/writes memory
MemoryStream memoryStream = new MemoryStream();

// Network stream - reads/writes over network
NetworkStream networkStream = tcpClient.GetStream();

// HTTP response stream
Stream httpStream = await response.Content.ReadAsStreamAsync();
```

### Why Use Streams?
✅ **Memory efficient** - doesn't load entire file into memory
✅ **Supports large files** - can process GB files
✅ **Async operations** - non-blocking I/O

### Stream Operations:
```csharp
var stream = new MemoryStream();

// Write data
await stream.WriteAsync(buffer, 0, buffer.Length);

// Read data
int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

// Seek (move position)
stream.Position = 0;  // Go to start
stream.Seek(100, SeekOrigin.Begin);  // Go to byte 100

// Properties
long length = stream.Length;  // Total size
long position = stream.Position;  // Current position
bool canRead = stream.CanRead;
bool canWrite = stream.CanWrite;
```

### Stream Disposal:
```csharp
// ✅ Using statement - auto-disposes
await using var stream = new MemoryStream();
// Stream is disposed here

// ❌ Manual disposal - error-prone
var stream = new MemoryStream();
stream.Dispose();  // Must remember to call
```

---

## 6. CancellationToken

### What is CancellationToken?
**Definition**: A mechanism to **cancel async operations** gracefully.

### Why Use It?
✅ **User cancellation** - user clicks "Cancel" button
✅ **Timeouts** - operation takes too long
✅ **Shutdown** - application is closing

### Basic Usage:
```csharp
public async Task DownloadAsync(CancellationToken cancellationToken)
{
    // Check if cancellation requested
    cancellationToken.ThrowIfCancellationRequested();
    
    // Pass to async methods
    await httpClient.SendAsync(request, cancellationToken);
    await stream.ReadAsync(buffer, cancellationToken);
}
```

### Creating CancellationToken:
```csharp
// 1. No cancellation (most common for console apps)
await DownloadAsync(CancellationToken.None);

// 2. Manual cancellation
var cts = new CancellationTokenSource();
await DownloadAsync(cts.Token);
cts.Cancel();  // Cancel the operation

// 3. Timeout cancellation
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await DownloadAsync(cts.Token);  // Auto-cancels after 30s
```

### Real Example from Our Code:
```csharp
public async Task DownloadAsync(AppOptions options, CancellationToken cancellationToken)
{
    // If user presses Ctrl+C, this will throw OperationCanceledException
    using var response = await httpClient.SendAsync(request, cancellationToken);
    
    // Cancellation is checked during read
    await contentStream.ReadAsync(buffer, cancellationToken);
}
```

---

## 7. Buffers

### What is a Buffer?
**Definition**: A **temporary storage area** in memory for data being transferred.

Think of it as a **bucket** that holds data while moving it from A to B.

### Why Use Buffers?
✅ **Performance** - reduces system calls
✅ **Efficiency** - processes data in chunks
✅ **Memory control** - limits memory usage

### Buffer Example:
```csharp
// Create a buffer (8KB)
var buffer = new byte[8192];

// Read data into buffer
int bytesRead = await stream.ReadAsync(buffer, cancellationToken);

// Buffer now contains data
// buffer[0] to buffer[bytesRead-1] have data
```

### Buffer Size Matters:
```csharp
// ❌ Too small - many system calls (slow)
var buffer = new byte[128];

// ✅ Optimal - balances memory and performance
var buffer = new byte[8192];  // 8KB

// ❌ Too large - wastes memory
var buffer = new byte[1024 * 1024];  // 1MB
```

### Our Code Example:
```csharp
var buffer = new byte[8192];  // 8KB buffer
int bytesRead;

while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
{
    // Write the chunk we just read
    await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
    totalBytesRead += bytesRead;
}
```

---

## 8. Async/Await

### What is Async/Await?
**Definition**: A pattern for writing **non-blocking** asynchronous code.

### Synchronous (Blocking):
```csharp
// ❌ Blocks the thread - UI freezes
public void DownloadFile()
{
    var data = httpClient.GetByteArrayAsync(url).Result;  // BLOCKS!
    File.WriteAllBytes("file.pdf", data);
}
```

### Asynchronous (Non-blocking):
```csharp
// ✅ Doesn't block - UI stays responsive
public async Task DownloadFileAsync()
{
    var data = await httpClient.GetByteArrayAsync(url);  // Doesn't block!
    await File.WriteAllBytesAsync("file.pdf", data);
}
```

### How It Works:
1. `async` keyword marks method as asynchronous
2. `await` keyword yields control back to caller
3. Method continues when operation completes

### Rules:
✅ `async` methods must return `Task` or `Task<T>`
✅ Use `await` for async operations
✅ Don't use `.Result` or `.Wait()` (causes deadlocks)

### Example:
```csharp
public async Task<int> GetFileSizeAsync(string url)
{
    // This doesn't block the thread
    var response = await httpClient.GetAsync(url);
    
    // This also doesn't block
    var content = await response.Content.ReadAsByteArrayAsync();
    
    return content.Length;
}
```

---

**Next**: [Part 2 - NuGet Packages & Libraries](GUIDE_02_PACKAGES.md)
