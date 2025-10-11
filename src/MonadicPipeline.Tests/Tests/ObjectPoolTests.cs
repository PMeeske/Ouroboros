using System.Text;
using LangChainPipeline.Core.Performance;
using Xunit;

namespace LangChainPipeline.Tests;

public class ObjectPoolTests
{
    [Fact]
    public void Rent_ShouldReturnNewObject_WhenPoolIsEmpty()
    {
        // Arrange
        var pool = new ObjectPool<StringBuilder>(() => new StringBuilder());

        // Act
        var obj = pool.Rent();

        // Assert
        Assert.NotNull(obj);
    }

    [Fact]
    public void Return_ShouldAddObjectToPool()
    {
        // Arrange
        var pool = new ObjectPool<StringBuilder>(() => new StringBuilder());
        var obj = pool.Rent();

        // Act
        pool.Return(obj);

        // Assert
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void Rent_ShouldReuseReturnedObject()
    {
        // Arrange
        var pool = new ObjectPool<StringBuilder>(() => new StringBuilder());
        var obj1 = pool.Rent();
        pool.Return(obj1);

        // Act
        var obj2 = pool.Rent();

        // Assert
        Assert.Same(obj1, obj2);
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Return_ShouldInvokeResetAction()
    {
        // Arrange
        bool resetCalled = false;
        var pool = new ObjectPool<StringBuilder>(
            () => new StringBuilder(),
            sb => resetCalled = true);
        var obj = pool.Rent();

        // Act
        pool.Return(obj);

        // Assert
        Assert.True(resetCalled);
    }

    [Fact]
    public void Return_ShouldNotExceedMaxPoolSize()
    {
        // Arrange
        var pool = new ObjectPool<StringBuilder>(
            () => new StringBuilder(),
            maxPoolSize: 2);

        // Act
        var obj1 = pool.Rent();
        var obj2 = pool.Rent();
        var obj3 = pool.Rent();
        pool.Return(obj1);
        pool.Return(obj2);
        pool.Return(obj3);

        // Assert
        Assert.True(pool.Count <= 2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllObjects()
    {
        // Arrange
        var pool = new ObjectPool<StringBuilder>(() => new StringBuilder());
        pool.Return(pool.Rent());
        pool.Return(pool.Rent());

        // Act
        pool.Clear();

        // Assert
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void RentDisposable_ShouldReturnToPoolOnDispose()
    {
        // Arrange
        var pool = new ObjectPool<StringBuilder>(() => new StringBuilder());

        // Act
        using (var pooled = pool.RentDisposable())
        {
            Assert.NotNull(pooled.Object);
            Assert.Equal(0, pool.Count);
        }

        // Assert
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void RentDisposable_ShouldAllowAccessToObject()
    {
        // Arrange
        var pool = new ObjectPool<StringBuilder>(() => new StringBuilder());

        // Act
        using var pooled = pool.RentDisposable();
        pooled.Object.Append("test");

        // Assert
        Assert.Equal("test", pooled.Object.ToString());
    }

    [Fact]
    public void CommonPools_StringBuilder_ShouldClearOnReturn()
    {
        // Arrange & Act
        StringBuilder sb;
        using (var pooled = CommonPools.StringBuilder.RentDisposable())
        {
            pooled.Object.Append("test");
            sb = pooled.Object;
        }

        // Assert - get the same object back
        using (var pooled = CommonPools.StringBuilder.RentDisposable())
        {
            if (ReferenceEquals(pooled.Object, sb))
            {
                Assert.Equal(0, pooled.Object.Length);
            }
        }
    }

    [Fact]
    public void CommonPools_StringList_ShouldClearOnReturn()
    {
        // Arrange & Act
        List<string> list;
        using (var pooled = CommonPools.StringList.RentDisposable())
        {
            pooled.Object.Add("test");
            list = pooled.Object;
        }

        // Assert - get the same object back
        using (var pooled = CommonPools.StringList.RentDisposable())
        {
            if (ReferenceEquals(pooled.Object, list))
            {
                Assert.Empty(pooled.Object);
            }
        }
    }

    [Fact]
    public void WithStringBuilder_ShouldExecuteActionAndReturn()
    {
        // Act
        var result = PooledHelpers.WithStringBuilder(sb =>
        {
            sb.Append("Hello");
            sb.Append(" ");
            sb.Append("World");
        });

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void WithStringList_ShouldExecuteFuncAndReturn()
    {
        // Act
        var result = PooledHelpers.WithStringList(list =>
        {
            list.Add("one");
            list.Add("two");
            list.Add("three");
            return string.Join(", ", list);
        });

        // Assert
        Assert.Equal("one, two, three", result);
    }

    [Fact]
    public void WithStringDictionary_ShouldExecuteFuncAndReturn()
    {
        // Act
        var result = PooledHelpers.WithStringDictionary(dict =>
        {
            dict["key1"] = "value1";
            dict["key2"] = "value2";
            return dict.Count;
        });

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void WithMemoryStream_ShouldExecuteFuncAndReturn()
    {
        // Act
        var result = PooledHelpers.WithMemoryStream(ms =>
        {
            using var writer = new StreamWriter(ms, leaveOpen: true);
            writer.Write("test content");
            writer.Flush();
            return ms.Length;
        });

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public async Task ObjectPool_ShouldBeThreadSafe()
    {
        // Arrange
        var pool = new ObjectPool<StringBuilder>(() => new StringBuilder(), maxPoolSize: 10);
        var tasks = new List<Task>();
        const int iterations = 100;

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    var obj = pool.Rent();
                    obj.Append("test");
                    pool.Return(obj);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - no exceptions thrown, pool is still functional
        var obj = pool.Rent();
        Assert.NotNull(obj);
    }
}
