using MelonLoader;
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.ItemFramework;
using Object = UnityEngine.Object;
using HarmonyPatch = HarmonyLib.HarmonyPatch;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Il2CppNewtonsoft.Json.Utilities;
using System.Linq;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Il2CppToolBuddy.Pooling.Collections;

[assembly: MelonInfo(typeof(ExpandRecipe.Main), "ExpandRecipe", "0.1.3", "Robb Manes", "https://github.com/robbmanes/Schedule1_ExpandRecipe")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ExpandRecipe
{
    public class Main : MelonMod
    {
        public string testedVersion = "0.3.4f4";
        public static ProductManager productManager;

        private static readonly List<string> bottomProducts = [
            "Granddaddy Purple",
            "OG Kush",
            "Green Crack",
            "Sour Diesel",
            "Meth",
            "Cocaine"
        ];

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

            foreach (List<StationRecipe.IngredientQuantity> list in retList)
            {
                list.Reverse();
            }

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
                                    StationRecipe.IngredientQuantity lastIngredient = new();
                                    if (checkRecipe.Ingredients.Contains(ingredient))
                                    {
                                        lastIngredient = checkIngredient;
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

        public static void BuildUIWithRecipe(StationRecipe.IngredientQuantity finalProduct, List<StationRecipe.IngredientQuantity> expandedRecipe, StationRecipe.IngredientQuantity baseProduct, GameObject recipesContainerUI)
        {
            // Use these to clone instead of doing it by hand
            GameObject recipeToCloneUI = recipesContainerUI.transform.Find("Recipe").gameObject ?? throw new Exception("Unable to find recipeUI GameObject");
            GameObject productToCloneUI = recipeToCloneUI.transform.Find("Product").gameObject ?? throw new Exception("Unable to find productUI GameObject");
            GameObject plusToCloneUI = recipeToCloneUI.transform.Find("Plus").gameObject ?? throw new Exception("Unable to find plusUI GameObject");
            GameObject mixerToCloneUI = recipeToCloneUI.transform.Find("Mixer").gameObject ?? throw new Exception("Unable to find mixerUI GameObject");
            GameObject arrowToCloneUI = recipeToCloneUI.transform.Find("Arrow").gameObject ?? throw new Exception("Unable to find arrowUI GameObject");
            GameObject outputToCloneUI = recipeToCloneUI.transform.Find("Output").gameObject ?? throw new Exception("Unable to find outputUI GameObject");

            // The recipesContainer rect holds 20 default recipes, and we need to add/manage just our own
            GameObject expandedRecipeUI;
            Transform expandedRecipeUITransform = recipesContainerUI.transform.Find("ExpandedRecipe");
            if (expandedRecipeUITransform == null)
            {
                expandedRecipeUI = Object.Instantiate(recipeToCloneUI, recipesContainerUI.transform).gameObject ?? throw new Exception("Failed to instantiate new ExpandedRecipeUI");
                expandedRecipeUI.name = "ExpandedRecipe";
                expandedRecipeUI.gameObject.SetActive(true);
                expandedRecipeUI.gameObject.AddComponent<HorizontalLayoutGroup>();
                expandedRecipeUI.transform.GetComponent<HorizontalLayoutGroup>().childScaleWidth = false;
            }
            else
            {
                expandedRecipeUI = expandedRecipeUITransform.gameObject;
                expandedRecipeUI.gameObject.SetActive(true);
            }

            // Always clear all children before using the recipe object
            int childCount = expandedRecipeUI.transform.GetChildCount();
            for (int i = childCount - 1; i >= 0; i--)
            {
                var child = expandedRecipeUI.transform.GetChild(i);
                GameObject.Destroy(child.gameObject);
            }

            GameObject baseProductUI = GameObject.Instantiate(productToCloneUI, expandedRecipeUI.transform).gameObject;
            baseProductUI.GetComponent<Image>().sprite = baseProduct.Item.Icon;
            baseProductUI.GetComponent<Image>().preserveAspect = true;
            baseProductUI.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = baseProduct.Item.name;

            int ingredientCount = expandedRecipe.Count - 1;
            foreach (StationRecipe.IngredientQuantity ingredient in expandedRecipe)
            {
                if (ingredientCount >= 0)
                {
                    GameObject plusClone = GameObject.Instantiate(plusToCloneUI, expandedRecipeUI.transform).gameObject;
                    plusClone.GetComponent<Image>().preserveAspect = true;
                }

                GameObject mixClone = GameObject.Instantiate(mixerToCloneUI, expandedRecipeUI.transform).gameObject;
                mixClone.GetComponent<Image>().sprite = ingredient.Item.Icon;
                mixClone.GetComponent<Image>().preserveAspect = true;
                mixClone.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = ingredient.Item.name;

                ingredientCount--;
            }

            GameObject arrowClone = GameObject.Instantiate(arrowToCloneUI, expandedRecipeUI.transform).gameObject;
            arrowClone.GetComponent<Image>().preserveAspect = true;

            GameObject outputClone = GameObject.Instantiate(outputToCloneUI, expandedRecipeUI.transform).gameObject;
            outputClone.GetComponent<Image>().sprite = finalProduct.Item.Icon;
            outputClone.GetComponent<Image>().preserveAspect = true;
            outputClone.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = finalProduct.Item.name;
        }
    }

    // Patch the products selected in the ProductManagerApp
    [HarmonyPatch(typeof(ProductManagerApp), "SelectProduct")]
    public static class ProductManager_SelectProduct_Patch
    {
        public static void Prefix(ProductManagerApp __instance, ProductEntry entry)
        {
            Transform recipesContainer;
            List<List<StationRecipe.IngredientQuantity>> expandedRecipesList;


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

                Transform existingExpandedRecipeEntry = recipesContainer.Find("ExpandedRecipe");
                if (existingExpandedRecipeEntry != null)
                {
                    // There's an existing entry - disable it
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
                Main.BuildUIWithRecipe(productEntry, expandedRecipes, recipesContainer.gameObject);

            } catch (Exception ex) {
                MelonLogger.Error($"Exception raised building UI component: {ex}");
                return;
            }
        }
    }
}