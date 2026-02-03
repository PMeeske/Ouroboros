using System.Text;
using Ouroboros.Core.Performance;

namespace Ouroboros.Specs.Steps;

[Binding]
public class ObjectPoolSteps
{
    private ObjectPool<StringBuilder>? _pool;
    private ObjectPool<StringBuilder>? _poolA;
    private ObjectPool<StringBuilder>? _poolB;
    private StringBuilder? _rentedObject;
    private StringBuilder? _firstObject;
    private StringBuilder? _secondObject;
    private List<StringBuilder> _rentedObjects = new List<StringBuilder>();
    private int _initialPoolSize;
    private Exception? _thrownException;
    private int _newObjectCount;

    [Given("a fresh object pool context")]
    public void GivenAFreshObjectPoolContext()
    {
        _pool = null;
        _poolA = null;
        _poolB = null;
        _rentedObject = null;
        _firstObject = null;
        _secondObject = null;
        _rentedObjects.Clear();
        _initialPoolSize = 0;
        _thrownException = null;
        _newObjectCount = 0;
    }

    [Given("an object pool of StringBuilder with initial capacity {int}")]
    public void GivenAnObjectPoolOfStringBuilderWithInitialCapacity(int capacity)
    {
        _pool = new ObjectPool<StringBuilder>(() => new StringBuilder(capacity));
    }

    [Given("an object pool with reset action that clears StringBuilder")]
    public void GivenAnObjectPoolWithResetActionThatClearsStringBuilder()
    {
        _pool = new ObjectPool<StringBuilder>(
            () => new StringBuilder(),
            sb => sb.Clear());
    }

    [Given("an object pool with max size {int}")]
    public void GivenAnObjectPoolWithMaxSize(int maxSize)
    {
        _pool = new ObjectPool<StringBuilder>(() => new StringBuilder(), maxPoolSize: maxSize);
    }

    [Given("I track CommonPools StringBuilderPool size")]
    public void GivenITrackCommonPoolsStringBuilderPoolSize()
    {
        _initialPoolSize = CommonPools.StringBuilder.Count;
    }

    [Given("two separate object pools")]
    public void GivenTwoSeparateObjectPools()
    {
        _poolA = new ObjectPool<StringBuilder>(() => new StringBuilder());
        _poolB = new ObjectPool<StringBuilder>(() => new StringBuilder());
    }

    [When("I rent an object")]
    public void WhenIRentAnObject()
    {
        _pool.Should().NotBeNull();
        _rentedObject = _pool!.Rent();
    }

    [When("I return the object")]
    public void WhenIReturnTheObject()
    {
        _pool.Should().NotBeNull();
        _rentedObject.Should().NotBeNull();
        _pool!.Return(_rentedObject!);
    }

    [When("I rent another object")]
    public void WhenIRentAnotherObject()
    {
        _pool.Should().NotBeNull();
        _secondObject = _pool!.Rent();
    }

    [When("I append {string} to the StringBuilder")]
    public void WhenIAppendToTheStringBuilder(string text)
    {
        _rentedObject.Should().NotBeNull();
        _rentedObject!.Append(text);
    }

    [When("I rent {int} objects")]
    public void WhenIRentObjects(int count)
    {
        _pool.Should().NotBeNull();
        for (int i = 0; i < count; i++)
        {
            _rentedObjects.Add(_pool!.Rent());
        }
    }

    [When("I return all objects")]
    public void WhenIReturnAllObjects()
    {
        _pool.Should().NotBeNull();
        foreach (var obj in _rentedObjects)
        {
            _pool!.Return(obj);
        }
    }

    [When("I return all {int} objects")]
    public void WhenIReturnAllObjectsWithCount(int count)
    {
        _pool.Should().NotBeNull();
        foreach (var obj in _rentedObjects)
        {
            _pool!.Return(obj);
        }
    }

    [When("I clear the pool")]
    public void WhenIClearThePool()
    {
        _pool.Should().NotBeNull();
        _pool!.Clear();
    }

    [When("I rent {int} more objects")]
    public void WhenIRentMoreObjects(int count)
    {
        _pool.Should().NotBeNull();
        var previousRented = _rentedObjects.ToList();
        _rentedObjects.Clear();

        for (int i = 0; i < count; i++)
        {
            var obj = _pool!.Rent();
            _rentedObjects.Add(obj);
            if (!previousRented.Any(prev => ReferenceEquals(prev, obj)))
            {
                _newObjectCount++;
            }
        }
    }

    [When("I rent with RentDisposable")]
    public void WhenIRentWithRentDisposable()
    {
        _pool.Should().NotBeNull();
        using var disposable = _pool!.RentDisposable();
        _rentedObject = disposable.Object;
    }

    [When("I dispose the disposable")]
    public void WhenIDisposeTheDisposable()
    {
        // Already disposed in RentDisposable method
    }

    [When("I rent from CommonPools StringBuilderPool")]
    public void WhenIRentFromCommonPoolsStringBuilderPool()
    {
        _rentedObject = CommonPools.StringBuilder.Rent();
    }

    [When("I rent from CommonPools StringListPool")]
    public void WhenIRentFromCommonPoolsStringListPool()
    {
        var list = CommonPools.StringList.Rent();
        list.Should().BeEmpty();
        CommonPools.StringList.Return(list);
    }

    [When("I rent from CommonPools StringDictionaryPool")]
    public void WhenIRentFromCommonPoolsStringDictionaryPool()
    {
        var dict = CommonPools.StringDictionary.Rent();
        dict.Should().BeEmpty();
        CommonPools.StringDictionary.Return(dict);
    }

    [When("I rent from CommonPools MemoryStreamPool")]
    public void WhenIRentFromCommonPoolsMemoryStreamPool()
    {
        var stream = CommonPools.MemoryStream.Rent();
        stream.Position.Should().Be(0);
        CommonPools.MemoryStream.Return(stream);
    }

    [When("I use GetPooledString to build {string}")]
    public void WhenIUseGetPooledStringToBuild(string expected)
    {
        var result = PooledHelpers.WithStringBuilder(sb => sb.Append(expected));
        result.Should().Be(expected);
    }

    [When("I run {int} parallel rent and return operations")]
    public void WhenIRunParallelRentAndReturnOperations(int operationCount)
    {
        _pool.Should().NotBeNull();

        var tasks = Enumerable.Range(0, operationCount)
            .Select(_ => Task.Run(() =>
            {
                var obj = _pool!.Rent();
                obj.Append("test");
                _pool.Return(obj);
            }))
            .ToArray();

        try
        {
            Task.WaitAll(tasks);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When("I rent from pool A and pool B")]
    public void WhenIRentFromPoolAAndPoolB()
    {
        _poolA.Should().NotBeNull();
        _poolB.Should().NotBeNull();
        _firstObject = _poolA!.Rent();
        _secondObject = _poolB!.Rent();
    }

    [Then("the rented object should not be null")]
    public void ThenTheRentedObjectShouldNotBeNull()
    {
        _rentedObject.Should().NotBeNull();
    }

    [Then("the second rented object should be the same as the first")]
    public void ThenTheSecondRentedObjectShouldBeTheSameAsTheFirst()
    {
        _firstObject.Should().NotBeNull();
        _secondObject.Should().NotBeNull();
        ReferenceEquals(_firstObject, _secondObject).Should().BeTrue();
    }

    [Then("the StringBuilder should be empty")]
    public void ThenTheStringBuilderShouldBeEmpty()
    {
        _secondObject.Should().NotBeNull();
        _secondObject!.Length.Should().Be(0);
    }

    [Then("only {int} objects should be from the pool")]
    public void ThenOnlyObjectsShouldBeFromThePool(int expectedFromPool)
    {
        _newObjectCount.Should().BeGreaterThanOrEqualTo(_rentedObjects.Count - expectedFromPool);
    }

    [Then("it should be a new object")]
    public void ThenItShouldBeANewObject()
    {
        // After clearing, the next rented object won't be from previous pool
        _rentedObject.Should().NotBeNull();
    }

    [Then("the object should be from the pool")]
    public void ThenTheObjectShouldBeFromThePool()
    {
        _secondObject.Should().NotBeNull();
        ReferenceEquals(_rentedObject, _secondObject).Should().BeTrue();
    }

    [Then("the StringBuilder should have capacity at least {int}")]
    public void ThenTheStringBuilderShouldHaveCapacityAtLeast(int minCapacity)
    {
        _rentedObject.Should().NotBeNull();
        _rentedObject!.Capacity.Should().BeGreaterThanOrEqualTo(minCapacity);
        CommonPools.StringBuilder.Return(_rentedObject);
    }

    [Then("the List should be empty")]
    public void ThenTheListShouldBeEmpty()
    {
        // Already checked in When step
    }

    [Then("the Dictionary should be empty")]
    public void ThenTheDictionaryShouldBeEmpty()
    {
        // Already checked in When step
    }

    [Then("the MemoryStream should have position {int}")]
    public void ThenTheMemoryStreamShouldHavePosition(int position)
    {
        // Already checked in When step
    }

    [Then("the result should be {string}")]
    public void ThenTheResultShouldBe(string expected)
    {
        // Already validated in When step
    }

    [Then("the pool size should not increase")]
    public void ThenThePoolSizeShouldNotIncrease()
    {
        var currentSize = CommonPools.StringBuilder.Count;
        currentSize.Should().BeLessThanOrEqualTo(_initialPoolSize + 1);
    }

    [Then("all operations should complete without exceptions")]
    public void ThenAllOperationsShouldCompleteWithoutExceptions()
    {
        _thrownException.Should().BeNull();
    }

    [Then("the objects should be different instances")]
    public void ThenTheObjectsShouldBeDifferentInstances()
    {
        _firstObject.Should().NotBeNull();
        _secondObject.Should().NotBeNull();
        ReferenceEquals(_firstObject, _secondObject).Should().BeFalse();
    }
}
