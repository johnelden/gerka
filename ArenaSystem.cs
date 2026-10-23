
using UnityEngine;

public class ArenaSystem : MonoBehaviour
{
    public Material grassMaterial;
    public Material riverMaterial;
    public Material woodMaterial;
    public Material stoneMaterial;
    public Material darkStoneMaterial;
    public Material leafMaterial;
    public Material flowerMaterial;
    public Material goldMaterial;

    Material lightStone;
    Material trunkMaterial;
    Material waterHighlight;
    Material pathMaterial;
    Material fieldInsetMaterial;
    Material fieldLineMaterial;

    public void BuildArena()
    {
        grassMaterial = Mat(new Color(0.29f, 0.46f, 0.24f));
        riverMaterial = Mat(new Color(0.08f, 0.38f, 0.62f));
        woodMaterial = Mat(new Color(0.38f, 0.20f, 0.09f));
        stoneMaterial = Mat(new Color(0.48f, 0.48f, 0.44f));
        darkStoneMaterial = Mat(new Color(0.26f, 0.28f, 0.28f));
        leafMaterial = Mat(new Color(0.16f, 0.31f, 0.23f));
        flowerMaterial = Mat(new Color(0.95f, 0.65f, 0.25f));
        goldMaterial = Mat(new Color(0.95f, 0.65f, 0.12f));

        lightStone = Mat(new Color(0.62f, 0.61f, 0.54f));
        trunkMaterial = Mat(new Color(0.29f, 0.16f, 0.08f));
        waterHighlight = Mat(new Color(0.28f, 0.68f, 0.85f));
        pathMaterial = Mat(new Color(0.48f, 0.40f, 0.29f));
        fieldInsetMaterial = Mat(new Color(0.32f, 0.49f, 0.25f));
        fieldLineMaterial = Mat(new Color(0.68f, 0.70f, 0.57f));

        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.58f, 0.68f, 0.65f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0035f;

        // Larger ground connects the arena to the surrounding forest.
        CreateBox("Forest Ground", new Vector3(0, -0.62f, 0),
            new Vector3(80, 0.35f, 80), grassMaterial);

        CreateBox("Arena Ground", new Vector3(0, -0.35f, 0),
            new Vector3(20, 0.5f, 36), grassMaterial);

        CreateFieldDetails();
        CreateBox("River", new Vector3(0, -0.06f, 0),
            new Vector3(20, 0.12f, 2.8f), riverMaterial);

        CreateRiverDetails();
        CreateBridge(-4.4f);
        CreateBridge(4.4f);
        CreatePaths();
        CreateBorders();
        CreateDecorations();
        CreateForest();
    }

    void CreateFieldDetails()
    {
        GameObject field = CreateBox("Arena Infield", new Vector3(0f, -0.085f, 0f),
            new Vector3(18.7f, 0.025f, 34.7f), fieldInsetMaterial);
        RemoveCollider(field);

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject sideline = CreateBox("Arena Field Line",
                new Vector3(side * 8.9f, -0.06f, 0f),
                new Vector3(0.045f, 0.012f, 33.8f), fieldLineMaterial);
            RemoveCollider(sideline);
        }
    }

    Material Mat(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogError("ArenaSystem: nie znaleziono zgodnego shadera.");
            return null;
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    public GameObject CreateBox(string objectName, Vector3 position,
        Vector3 scale, Material material, Transform parent = null)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = objectName;
        obj.transform.position = position;
        obj.transform.localScale = scale;

        if (parent != null)
            obj.transform.SetParent(parent, true);

        obj.GetComponent<Renderer>().material = material;
        return obj;
    }

    GameObject Sphere(string objectName, Vector3 position,
        Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = objectName;
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().material = material;
        RemoveCollider(obj);
        return obj;
    }

    void RemoveCollider(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    void CreateRiverDetails()
    {
        for (int i = 0; i < 30; i++)
        {
            float x = -8.5f + i * 0.58f;

            if (Mathf.Abs(x + 4.4f) < 1.8f ||
                Mathf.Abs(x - 4.4f) < 1.8f)
                continue;

            GameObject ripple = CreateBox("Water Ripple",
                new Vector3(x, 0.015f, Mathf.Sin(i * 1.7f) * 0.7f),
                new Vector3(0.38f, 0.015f, 0.045f), waterHighlight);

            ripple.transform.rotation = Quaternion.Euler(
                0, i % 2 == 0 ? 8f : -8f, 0);
            RemoveCollider(ripple);
        }

        for (int i = 0; i < 22; i++)
        {
            float x = -8.5f + i * 0.8f;

            if (Mathf.Abs(x + 4.4f) < 1.8f ||
                Mathf.Abs(x - 4.4f) < 1.8f)
                continue;

            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox("Riverbank Stone",
                    new Vector3(x, 0.03f, side * 1.52f),
                    new Vector3(0.62f, 0.20f, 0.30f),
                    i % 2 == 0 ? stoneMaterial : darkStoneMaterial);
            }
        }
    }

    void CreateBridge(float x)
    {
        GameObject bridge = new GameObject(
            x < 0 ? "Left Wooden Bridge" : "Right Wooden Bridge");

        CreateBox("Bridge Foundation", new Vector3(x, 0.08f, 0),
            new Vector3(3.15f, 0.28f, 4.1f),
            darkStoneMaterial, bridge.transform);

        // Separate planks, not a single flat wooden block.
        for (int i = 0; i < 17; i++)
        {
            float z = -1.88f + i * 0.235f;

            GameObject plank = CreateBox("Wooden Bridge Plank",
                new Vector3(x, 0.27f, z),
                new Vector3(2.95f, 0.13f, 0.205f),
                woodMaterial, bridge.transform);

            plank.transform.rotation = Quaternion.Euler(
                0, 0, (i % 3 - 1) * 1.2f);
        }

        for (int i = 0; i < 5; i++)
        {
            CreateBox("Bridge Crossbeam",
                new Vector3(x, 0.12f, -1.8f + i * 0.9f),
                new Vector3(3.35f, 0.22f, 0.22f),
                darkStoneMaterial, bridge.transform);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            float railX = x + side * 1.58f;

            CreateBox("Bridge Wooden Rail",
                new Vector3(railX, 0.70f, 0),
                new Vector3(0.15f, 0.16f, 4f),
                woodMaterial, bridge.transform);

            for (int i = 0; i < 5; i++)
            {
                float z = -1.75f + i * 0.875f;

                CreateBox("Bridge Rail Post",
                    new Vector3(railX, 0.48f, z),
                    new Vector3(0.19f, 0.58f, 0.19f),
                    lightStone, bridge.transform);

                CreateBox("Bridge Post Cap",
                    new Vector3(railX, 0.80f, z),
                    new Vector3(0.25f, 0.12f, 0.25f),
                    goldMaterial, bridge.transform);
            }
        }

        for (int side = -1; side <= 1; side += 2)
        {
            CreateBox("Bridge Stone Approach",
                new Vector3(x, 0.05f, side * 2.35f),
                new Vector3(3.4f, 0.18f, 0.85f),
                stoneMaterial, bridge.transform);
        }
    }

    void CreatePaths()
    {
        foreach (float x in new float[] { -4.4f, 4.4f })
        {
            CreateBox("Player Path", new Vector3(x, -0.04f, -6.2f),
                new Vector3(3.2f, 0.12f, 9.2f), pathMaterial);

            CreateBox("Enemy Path", new Vector3(x, -0.04f, 6.2f),
                new Vector3(3.2f, 0.12f, 9.2f), pathMaterial);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            for (int lane = 0; lane < 2; lane++)
            {
                float x = lane == 0 ? -4.4f : 4.4f;

                for (int i = 0; i < 7; i++)
                {
                    float z = side * (2.9f + i * 0.7f);

                    GameObject p = CreateBox("Path Paving Stone",
                        new Vector3(x + Mathf.Sin(i * 2.1f) * 0.65f,
                            0.04f, z),
                        new Vector3(0.48f, 0.12f, 0.42f),
                        i % 2 == 0 ? stoneMaterial : pathMaterial);

                    p.transform.rotation = Quaternion.Euler(
                        0, i % 2 == 0 ? 8f : -12f, 0);
                }
            }
        }
    }

    void CreateBorders()
    {
        for (int side = -1; side <= 1; side += 2)
        {
            CreateBox("Arena Side Border",
                new Vector3(side * 9.8f, 0.05f, 0),
                new Vector3(0.4f, 0.25f, 36f), darkStoneMaterial);

            for (int i = 0; i < 14; i++)
            {
                CreateBox("Border Stone",
                    new Vector3(side * 9.45f, 0.13f, -16.2f + i * 2.5f),
                    new Vector3(0.4f, 0.24f, 0.9f), stoneMaterial);
            }
        }

        CreateBox("North Border", new Vector3(0, 0.05f, 17.7f),
            new Vector3(20f, 0.25f, 0.4f), darkStoneMaterial);

        CreateBox("South Border", new Vector3(0, 0.05f, -17.7f),
            new Vector3(20f, 0.25f, 0.4f), darkStoneMaterial);
    }

    void CreateDecorations()
    {
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 10; i++)
            {
                float z = -15.2f + i * 3.35f;
                Tree(new Vector3(side * 11.2f, 0, z));

                if (i % 3 == 0)
                    Bush(new Vector3(side * 9.6f, 0, z + 0.7f));

                if (i % 4 == 0)
                    Barrel(new Vector3(side * 8.4f, 0, z - 0.55f));
            }

            for (int i = 0; i < 9; i++)
            {
                Vector3 p = new Vector3(
                    side * Random.Range(7.2f, 9.2f), 0,
                    Random.Range(-14.5f, 14.5f));

                if (Mathf.Abs(p.z) > 2.5f)
                    FlowerPatch(p);
            }

            Torch(new Vector3(side * 3f, 0, -2.8f));
            Torch(new Vector3(side * 3f, 0, 2.8f));
        }

        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(-8f, 8f);

            if (Mathf.Abs(x - 4.4f) < 2f ||
                Mathf.Abs(x + 4.4f) < 2f)
                continue;

            float z = (i % 2 == 0 ? -1f : 1f) *
                      Random.Range(1.65f, 2.15f);

            Sphere("Riverbank Rock", new Vector3(x, 0.08f, z),
                new Vector3(Random.Range(0.25f, 0.65f),
                    Random.Range(0.18f, 0.38f),
                    Random.Range(0.25f, 0.55f)), darkStoneMaterial);
        }
    }

    void Tree(Vector3 p)
    {
        CreateBox("Tree Trunk", p + Vector3.up * 0.7f,
            new Vector3(0.32f, 1.4f, 0.32f), trunkMaterial);

        Sphere("Tree Crown", p + Vector3.up * 1.8f,
            new Vector3(1.45f, 1.8f, 1.45f), leafMaterial);

        Sphere("Tree Crown Top", p + Vector3.up * 2.5f,
            new Vector3(1f, 1.1f, 1f),
            Mat(new Color(0.18f, 0.42f, 0.15f)));
    }

    void Bush(Vector3 p)
    {
        for (int i = 0; i < 3; i++)
        {
            Sphere("Bush", p + new Vector3((i - 1) * 0.34f,
                0.28f, Mathf.Sin(i * 2f) * 0.15f),
                new Vector3(0.7f, 0.55f, 0.7f), leafMaterial);
        }
    }

    void FlowerPatch(Vector3 p)
    {
        for (int i = 0; i < 3; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-0.25f, 0.25f), 0,
                Random.Range(-0.25f, 0.25f));

            CreateBox("Flower Stem", p + offset + Vector3.up * 0.12f,
                new Vector3(0.035f, 0.24f, 0.035f), leafMaterial);

            Sphere("Flower", p + offset + Vector3.up * 0.27f,
                Vector3.one * 0.16f, flowerMaterial);
        }
    }

    void Barrel(Vector3 p)
    {
        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Wooden Barrel";
        barrel.transform.position = p + Vector3.up * 0.35f;
        barrel.transform.localScale = new Vector3(0.48f, 0.35f, 0.48f);
        barrel.GetComponent<Renderer>().material = woodMaterial;

        for (int i = -1; i <= 1; i += 2)
        {
            CreateBox("Barrel Band",
                p + new Vector3(0, 0.35f + i * 0.19f, 0),
                new Vector3(0.49f, 0.055f, 0.49f), darkStoneMaterial);
        }
    }

    void Torch(Vector3 p)
    {
        CreateBox("Torch Pole", p + Vector3.up * 0.55f,
            new Vector3(0.14f, 1.1f, 0.14f), trunkMaterial);

        CreateBox("Torch Holder", p + Vector3.up,
            new Vector3(0.28f, 0.12f, 0.28f), darkStoneMaterial);

        Sphere("Torch Flame", p + Vector3.up * 1.22f,
            new Vector3(0.22f, 0.34f, 0.22f),
            Mat(new Color(1f, 0.43f, 0.06f)));

        GameObject lightObject = new GameObject("Torch Glow");
        Light glow = lightObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.45f, 0.12f);
        glow.intensity = 1.1f;
        glow.range = 4f;
        glow.transform.position = p + Vector3.up * 1.4f;
    }

    void CreateForest()
    {
        // Sparse tree lines leave the arena edges and towers unobstructed.
        for (int i = 0; i < 22; i++)
        {
            float z = -30f + i * 2.8f;
            float x = 12f + (i % 3) * 1.6f;

            Tree(new Vector3(x, 0, z));
            Tree(new Vector3(-x, 0, z));

            if (i % 4 == 0)
            {
                Bush(new Vector3(x - 1.25f, 0, z + 0.55f));
                Bush(new Vector3(-x + 1.25f, 0, z + 0.55f));
            }
        }

        // Extra trees immediately outside the north and south arena borders.
        for (int i = 0; i < 12; i++)
        {
            float x = -16f + i * 2.9f;
            float northZ = 18.5f + (i % 3) * 0.7f;
            float southZ = -18.5f - (i % 3) * 0.7f;

            Tree(new Vector3(x, 0, northZ));
            Tree(new Vector3(x, 0, southZ));

            if (i % 3 == 0)
            {
                Bush(new Vector3(x + 0.65f, 0, northZ + 0.7f));
                Bush(new Vector3(x - 0.65f, 0, southZ - 0.7f));
            }
        }

        // A second, staggered tree line creates a fuller forest backdrop.
        for (int i = 0; i < 11; i++)
        {
            float x = -18f + i * 3.6f;
            Tree(new Vector3(x, 0, 25f + (i % 2) * 0.8f));
            Tree(new Vector3(x, 0, -25f - (i % 2) * 0.8f));
        }

        // Distant tree line gives the scene a natural horizon.
        for (int i = 0; i < 18; i++)
        {
            float x = -28f + i * 3.2f;
            Tree(new Vector3(x, 0, 32f));
            Tree(new Vector3(x, 0, -32f));
        }
    }
}
