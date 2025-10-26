using System;
using System.Linq.Expressions;
using System.Reflection;

namespace ClearFlow.StatePersistence.Aggregates;

public class TrackedModel<TModel>
{
    public TModel Model { get; private set; }
    public bool HasChanged { get; private set; }
    public bool IsDeleted { get; private set; }
    public bool IsNew { get; private set; }
    public long Version { get; private set; }

    public TrackedModel(TModel model, bool hasChanged, bool isDeleted, bool isNew, long version)
    {
        Model = model;
        Version = version;
        HasChanged = hasChanged;
        IsDeleted = isDeleted;
        IsNew = isNew;
    }

    public static TrackedModel<TModel> From(TModel model, long version)
    {
        return new TrackedModel<TModel>(model, false, false, false, version);
    }

    public static TrackedModel<TModel> Replace(TModel model, long version)
    {
        return new TrackedModel<TModel>(model, true, false, false, version);
    }

    public static TrackedModel<TModel> FromEmptyState(TModel model)
    {
        return new TrackedModel<TModel>(model, false, false, true, 0);
    }

    public static TrackedModel<TModel> FromExistingState(TModel model)
    {
        return new TrackedModel<TModel>(model, false, false, true, 1);
    }

    public TrackedModel<TModel> MarkForDeletion()
    {
        HasChanged = false;
        IsDeleted = true;

        return this;
    }

    public TrackedModel<TModel> SetValue<TProperty>(Expression<Func<TModel, TProperty>> expression, TProperty value)
    {
        var propertyInfo = GetPropertyInfo(expression);
        propertyInfo.SetValue(Model, value);

        if (IsNew) 
        {
            Version++;
        }

        HasChanged = true;

        return this;
    }

    private static PropertyInfo GetPropertyInfo<TResult>(Expression<Func<TModel, TResult>> expr)
    {
        var memberAccess = expr.Body as MemberExpression;
        var propertyInfo = memberAccess?.Member as PropertyInfo;
        if (propertyInfo == null)
        {
            throw new InvalidOperationException("SetValue can only be used with properties.");
        }

        return propertyInfo;
    }
}
