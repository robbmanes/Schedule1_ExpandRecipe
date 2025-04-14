using MelonLoader;
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.ItemFramework;
using Object = UnityEngine.Object;
using HarmonyPatch = HarmonyLib.HarmonyPatch;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(ExpandRecipe.Main), "ExpandRecipe", "0.2.1", "Robb Manes", "https://github.com/robbmanes/Schedule1_ExpandRecipe")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ExpandRecipe
{
    public class Main : MelonMod
    {
        public string testedVersion = "0.3.4f4";
        public static ProductManager productManager;
        public static ProductManagerApp productManagerApp;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg($"Tested on Schedule I version \"{testedVersion}\"");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Main")
            {
                // Make sure we can find the ProductManager
                try
                {
                    productManager = Object.FindObjectsOfType<ProductManager>()[0];
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to find base Product Manager: {ex}");
                }
            }
            base.OnSceneWasLoaded(buildIndex, sceneName);
        }

        /* Product Recipe Handling */
        // ProductDefinition.Recipes contains a List<StationRecipe>
        // Multiple recipe lists can exist for a single ProductDefinition
        // Each StationRecipe contains a StationRecipe.IngredientQuantity
        // Each IngredientQuantity needs to be evaluated as either a base ingredient or a product
        // If it's a product, we need to recurse again

        // Public method to present the lists
        public static List<List<StationRecipe.IngredientQuantity>> GetExpandedRecipes(ProductEntry productEntry)
        {
            Il2CppSystem.Collections.Generic.List<StationRecipe> baseRecipes = productEntry.Definition.Recipes;
            List<List<StationRecipe.IngredientQuantity>> retList = new();

            retList = GetExpandedRecipesInternal(baseRecipes);
            return retList;
        }

        // Take a starting productDefintion and return a properly ordered list of *all* possible recipes
        // Each item in the returned list should be an ordered path to the same product
        private static List<List<StationRecipe.IngredientQuantity>> GetExpandedRecipesInternal(Il2CppSystem.Collections.Generic.List<StationRecipe> baseRecipes)
        {
            // Unsure why this must be the case, check compatibility?
            List<List<StationRecipe.IngredientQuantity>> retList = new();

            // No recipes means we bail early
            if (baseRecipes.Count > 0)
            {
                foreach (StationRecipe recipe in baseRecipes)
                {
                    // Order our list to be base mixers first, always
                    // Not doing this causes out-of-order problems
                    // No List.OrderBy in IL2Cpp?
                    List<StationRecipe.IngredientQuantity> sortedIngredients = new List<StationRecipe.IngredientQuantity>();
                    foreach (StationRecipe.IngredientQuantity ingredient in recipe.Ingredients)
                    {
                        if (ingredient.Item.Category == EItemCategory.Product)
                        {
                            sortedIngredients = sortedIngredients.Append(ingredient).ToList();
                        }
                        else
                        {
                            sortedIngredients = sortedIngredients.Prepend(ingredient).ToList();
                        }
                    }

                    // Once we're properly ordered, we can process ingredients
                    foreach (StationRecipe.IngredientQuantity ingredient in sortedIngredients)
                    {
                        bool hitBottom = false;

                        if (ingredient.Item.Category == EItemCategory.Product)
                        {
                            // We are a product, do a lookup
                            Func<ProductDefinition, bool> value = x => x.ID == ingredient.Item.ID;
                            var product = productManager.AllProducts.Find(value) ?? throw new Exception($"Could not find base product for \"'{ingredient.Item.Name}'\"");

                            // Check if product has no recipes to iterate over
                            if (product.Recipes.Count <= 0)
                            {
                                // We have to be the last on the stack, go back up one entry on the list
                                // and add ourselves as the final product
                                if (retList.Count > 0)
                                {
                                    if (retList[retList.Count - 1].Count > 0)
                                    {
                                        retList[retList.Count - 1] = retList[retList.Count - 1].Prepend(ingredient).ToList();
                                    }
                                }
                                hitBottom = true;
                                break;
                            }

                            // Check the upcoming product recipe for looping
                            for (int i = 0; i < product.Recipes.Count; i++)
                            {
                                var checkRecipe = product.Recipes[i];
                                foreach (StationRecipe.IngredientQuantity checkIngredient in checkRecipe.Ingredients)
                                {
                                    // Does our next recipe contain ourselves?
                                    if (checkRecipe.Ingredients.Contains(ingredient))
                                    {
                                        // We're going to loop if we keep going, we've hit bottom
                                        //
                                        // This is a bit tricky because our last mixer is (always?) going to be the start
                                        // of the loop.  We probably want to eventually clean this up but I haven't
                                        // covered all of the corner cases yet.
                                        //
                                        // Add ourselves as the final base product overwriting the mixer and quit
                                        if (retList.Count > 0)
                                        {
                                            if (retList[retList.Count - 1].Count > 0)
                                            {
                                                retList[retList.Count - 1] = retList[retList.Count - 1].Prepend(ingredient).ToList();
                                            }
                                        }
                                        hitBottom = true;
                                        break;
                                    }
                                }
                            }

                            // We've already hit bottom, stop the whole loop before we recurse again
                            if (hitBottom) break;

                            // Recurse into our new product and get its recipes
                            var recursiveRecipes = GetExpandedRecipesInternal(product.Recipes);
                            foreach (List<StationRecipe.IngredientQuantity> recursiveRecipe in recursiveRecipes)
                            {
                                // Add all previous mixers
                                recursiveRecipe.AddRange(sortedIngredients);

                                // Remove our intermediate product as we've been expanded to mixers
                                recursiveRecipe.Remove(ingredient);

                                // Add our sanitized list(s) onto the retList
                                retList.Add(recursiveRecipe);
                            }
                        }
                        else
                        {
                            List<StationRecipe.IngredientQuantity> ingredientList = new();
                            ingredientList.Add(ingredient);
                            retList.Add(ingredientList);
                        }
                    }
                }
            }

            // Prior to returning our list of lists, we want to remove subset lists that aren't complete recipes
            foreach (List<StationRecipe.IngredientQuantity> listA in retList.ToList())
            {
                foreach (List<StationRecipe.IngredientQuantity> listB in retList.ToList())
                {
                    if (listA == listB)
                    {
                        // This is our same list, skip this operation
                        continue;
                    }

                    if (!listB.Except(listA).Any())
                    {
                        // Check if there are differences to avoid deleting a branching path
                        if (listB.Except(listA).ToList().Count > 0)
                        {
                            // We're a unique branch, don't delete
                            continue;
                        }
                        retList.Remove(listB);
                    }
                }
            }
            return retList;
        }

        /* UI and ProductManagerApp Handling */
        public static GameObject CreateExpandedRecipesTextUIGameObject(GameObject gameObjectToClone, GameObject parentGameObject)
        {
            GameObject expandedRecipesTextUI;
            Transform expandedRecipesTextUITransform = parentGameObject.transform.Find("ExpandedRecipesText");
            if (expandedRecipesTextUITransform == null)
            {
                expandedRecipesTextUI = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
                expandedRecipesTextUI.name = "ExpandedRecipesText";
                expandedRecipesTextUI.GetComponent<Text>().text = "Expanded Recipe(s)";
            }
            else
            {
                expandedRecipesTextUI = expandedRecipesTextUITransform.gameObject;
            }

            expandedRecipesTextUI.gameObject.SetActive(true);

            return expandedRecipesTextUI;
        }

        public static GameObject GetOrCreateExpandedRecipesUIGameObject(GameObject parentGameObject)
        {
            GameObject expandedRecipesUI;
            Transform expandedRecipeUITransform = parentGameObject.transform.Find("ExpandedRecipes");
            if (expandedRecipeUITransform == null)
            {
                expandedRecipesUI = GameObject.Instantiate(new GameObject(), parentGameObject.transform).gameObject;
                expandedRecipesUI.name = "ExpandedRecipes";

                VerticalLayoutGroup verticalLayoutGroup = expandedRecipesUI.gameObject.AddComponent<VerticalLayoutGroup>();
                verticalLayoutGroup.spacing = 8;
                verticalLayoutGroup.childScaleHeight = false;
                verticalLayoutGroup.childScaleWidth = false;
                verticalLayoutGroup.childControlHeight = false;
                verticalLayoutGroup.childControlWidth = false;
                verticalLayoutGroup.childForceExpandHeight = false;
                verticalLayoutGroup.childForceExpandWidth = false;
            }
            else
            {
                expandedRecipesUI = expandedRecipeUITransform.gameObject;

                // Clear existing recipes
                expandedRecipesUI.DestroyChildren();
            }
            expandedRecipesUI.gameObject.SetActive(true);

            return expandedRecipesUI;
        }

        public static GameObject CreateExpandedRecipeUIGameObject(GameObject gameObjectToClone, GameObject parentGameObject)
        {
            GameObject expandedRecipeUI = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
            expandedRecipeUI.gameObject.SetActive(true);

            HorizontalLayoutGroup horizontalLayoutGroup = expandedRecipeUI.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
            horizontalLayoutGroup.childScaleHeight = false;
            horizontalLayoutGroup.childScaleWidth = false;
            horizontalLayoutGroup.childControlHeight = false;
            horizontalLayoutGroup.childControlWidth = true;
            horizontalLayoutGroup.childForceExpandHeight = false;
            horizontalLayoutGroup.childForceExpandWidth = false;

            // Clear cloned recipe
            expandedRecipeUI.DestroyChildren();

            return expandedRecipeUI;
        }

        public static GameObject CreateBaseProductUIGameObject(GameObject gameObjectToClone, GameObject parentGameObject, List<StationRecipe.IngredientQuantity> expandedRecipe)
        {
            // First ingredient on the list is the base product
            var baseProduct = expandedRecipe.First();
            GameObject baseProductUI = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
            baseProductUI.GetComponent<Image>().sprite = baseProduct.Item.Icon;
            baseProductUI.GetComponent<Image>().preserveAspect = true;
            baseProductUI.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = baseProduct.Item.name;

            return baseProductUI;
        }

        public static GameObject CreatePlusUIGameObject(GameObject gameObjectToClone, GameObject parentGameObject)
        {
            GameObject plusClone = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
            plusClone.GetComponent<Image>().preserveAspect = true;
            return plusClone;
        }

        public static GameObject CreateMixUIGameObject(GameObject gameObjectToClone, GameObject parentGameObject, StationRecipe.IngredientQuantity ingredient)
        {
            GameObject mixClone = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
            mixClone.GetComponent<Image>().sprite = ingredient.Item.Icon;
            mixClone.GetComponent<Image>().preserveAspect = true;
            mixClone.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = ingredient.Item.name;

            return mixClone;
        }

        public static GameObject CreateArrowUIGameObject(GameObject gameObjectToClone, GameObject parentGameObject)
        {
            GameObject arrowClone = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
            return arrowClone;
        }

        public static GameObject CreateOutputUIGameObject(GameObject gameObjectToClone, GameObject parentGameObject, StationRecipe.ItemQuantity finalProduct)
        {
            GameObject outputClone = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject; outputClone.GetComponent<Image>().sprite = finalProduct.Item.Icon;
            outputClone.GetComponent<Image>().preserveAspect = true;
            outputClone.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = finalProduct.Item.name;

            return outputClone;
        }

        public static void BuildUIWithRecipe(ProductEntry productEntry, List<List<StationRecipe.IngredientQuantity>> expandedRecipes, GameObject recipeTextUI, GameObject recipesContainerUI)
        {
            // Get the first product in the first recipe to convert to IngredientQuantity
            var product = productEntry.Definition.Recipes[0].Product;

            // Use these to clone instead of doing it by hand
            GameObject recipeToCloneUI = recipesContainerUI.transform.Find("Recipe").gameObject ?? throw new Exception("Unable to find recipeUI GameObject");
            GameObject productToCloneUI = recipeToCloneUI.transform.Find("Product").gameObject ?? throw new Exception("Unable to find productUI GameObject");
            GameObject plusToCloneUI = recipeToCloneUI.transform.Find("Plus").gameObject ?? throw new Exception("Unable to find plusUI GameObject");
            GameObject mixerToCloneUI = recipeToCloneUI.transform.Find("Mixer").gameObject ?? throw new Exception("Unable to find mixerUI GameObject");
            GameObject arrowToCloneUI = recipeToCloneUI.transform.Find("Arrow").gameObject ?? throw new Exception("Unable to find arrowUI GameObject");
            GameObject outputToCloneUI = recipeToCloneUI.transform.Find("Output").gameObject ?? throw new Exception("Unable to find outputUI GameObject");

            CreateExpandedRecipesTextUIGameObject(recipeTextUI, recipesContainerUI);

            // The recipesContainer rect holds 20 default recipes, and we need to add/manage just our own
            GameObject expandedRecipesUI = GetOrCreateExpandedRecipesUIGameObject(recipesContainerUI);

            foreach (List<StationRecipe.IngredientQuantity> expandedRecipe in expandedRecipes)
            {
                GameObject expandedRecipeUI = CreateExpandedRecipeUIGameObject(recipeToCloneUI, expandedRecipesUI);

                CreateBaseProductUIGameObject(productToCloneUI, expandedRecipeUI, expandedRecipe);

                // Once we've added the base product, strip off the first entry
                expandedRecipe.Remove(expandedRecipe[0]);

                int ingredientCount = expandedRecipe.Count;
                foreach (StationRecipe.IngredientQuantity ingredient in expandedRecipe)
                {
                    if (ingredientCount >= 0)
                    {
                        CreatePlusUIGameObject(plusToCloneUI, expandedRecipeUI);
                    }

                    CreateMixUIGameObject(mixerToCloneUI, expandedRecipeUI, ingredient);

                    ingredientCount--;
                }

                CreateArrowUIGameObject(arrowToCloneUI, expandedRecipeUI);
                CreateOutputUIGameObject(outputToCloneUI, expandedRecipeUI, product);
            }
        }
    }

    /* Unity and GameObject extensions */
    public static class GameObjectExtensions
    {
        public static void DestroyChildren(this GameObject thisGameObject)
        {
            int childCount = thisGameObject.transform.GetChildCount();
            for (int i = childCount - 1; i >= 0; i--)
            {
                var child = thisGameObject.transform.GetChild(i);
                GameObject.Destroy(child.gameObject);
            }
        }
    }

    /* Harmony Patches and Injections/Event Handling */
    // Patch the products selected in the ProductManagerApp
    [HarmonyPatch(typeof(ProductManagerApp), "SelectProduct")]
    public static class ProductManager_SelectProduct_Patch
    {
        public static void Prefix(ProductManagerApp __instance, ProductEntry entry)
        {
            List<List<StationRecipe.IngredientQuantity>> expandedRecipesList;
            Transform recipesContainer;
            Transform recipeText;
            Main.productManagerApp = __instance;

            // Set up the Phone UI
            // We have to do this early to clear existing ExpandedUI entries if they exist
            try
            {
                var detailPanel = __instance.DetailPanel;
                recipesContainer = detailPanel.transform.Find("Scroll View/Viewport/Content/RecipesContainer");
                if (recipesContainer == null)
                {
                    MelonLogger.Error("Can't find RecipesContainer object in current scene");
                    return;
                }

                recipeText = detailPanel.transform.Find("Scroll View/Viewport/Content/Recipes");
                if (recipeText == null)
                {
                    MelonLogger.Error("Can't find Recipes object in current scene");
                    return;
                }

                Transform existingExpandedRecipeEntry = recipesContainer.Find("ExpandedRecipes");
                if (existingExpandedRecipeEntry != null)
                {
                    // There's an existing object already - disable it
                    existingExpandedRecipeEntry.gameObject.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to find Phone UI components: {ex}");
                return;
            }

            // Get a list of all recipes for a given base product
            try
            {
                expandedRecipesList = Main.GetExpandedRecipes(entry);

                // Print the result to the console
                foreach (List<StationRecipe.IngredientQuantity> recipe in expandedRecipesList)
                {
                    string endProductName = entry.Definition.Name;
                    string recipeString = "";
                    foreach (StationRecipe.IngredientQuantity ingredient in recipe)
                    {
                        if (recipeString.Length > 0)
                        {
                            recipeString += " + ";
                        }
                        recipeString += $"{ingredient.Item.name}";
                    }
                    MelonLogger.Msg($"Expanded Recipe for \"{endProductName}\": {recipeString}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to get expanded recipe list: {ex}");
                return;
            }

            // Actually update the UI
            try
            {
                Main.BuildUIWithRecipe(entry, expandedRecipesList, recipeText.gameObject, recipesContainer.gameObject);

            } catch (Exception ex) {
                MelonLogger.Error($"Exception raised building UI component: {ex}");
                return;
            }
        }
    }
}