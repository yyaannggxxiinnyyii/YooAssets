using UnityEngine;

/// <summary>
/// 泛型 MonoBehaviour 单例模板。
/// 用法：public class Foo : Singleton&lt;Foo&gt;
/// </summary>
/// <typeparam name="T">需要实现单例的 MonoBehaviour 子类</typeparam>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindObjectOfType<T>();
            }

            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
            _instance = this as T;
        else if (_instance != this)
            Destroy(gameObject);
    }
}
