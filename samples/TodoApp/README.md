# TodoApp Sample

A complete task management application demonstrating OpenMaui Linux capabilities with real-world XAML patterns.

## Features

- **NavigationPage** - Full page navigation with back button support
- **CollectionView** - Scrollable list with data binding and selection
- **XAML Data Binding** - Value converters for dynamic styling
- **DisplayAlert Dialogs** - Confirmation dialogs for delete actions
- **Grid Layouts** - Complex layouts with star sizing for expanding content
- **Entry & Editor** - Single and multi-line text input
- **Border with RoundRectangle** - Modern card-style UI
- **ToolbarItems** - Navigation bar actions

## Screenshots

The app consists of three pages:

1. **TodoListPage** - Shows all tasks with completion status indicators
2. **NewTodoPage** - Create a new task with title and notes
3. **TodoDetailPage** - View/edit task details, mark complete, or delete

## Architecture

```
TodoApp/
├── App.cs                    # Application entry with NavigationPage
├── Program.cs                # Linux platform bootstrap
├── MauiProgram.cs            # MAUI app builder
├── TodoItem.cs               # Data model
├── TodoService.cs            # In-memory data store
├── TodoListPage.xaml(.cs)    # Main list view
├── NewTodoPage.xaml(.cs)     # Create task page
└── TodoDetailPage.xaml(.cs)  # Task detail/edit page
```

## XAML Highlights

### Value Converters
The app uses custom converters for dynamic styling based on completion status:
- `CompletedToColorConverter` - Gray text for completed items
- `CompletedToTextDecorationsConverter` - Strikethrough for completed items
- `CompletedToOpacityConverter` - Fade completed items
- `AlternatingRowColorConverter` - Alternating background colors

### ResourceDictionary
```xml
<ContentPage.Resources>
    <Color x:Key="PrimaryColor">#5C6BC0</Color>
    <Color x:Key="AccentColor">#26A69A</Color>
    <Color x:Key="TextPrimary">#212121</Color>
</ContentPage.Resources>
```

### CollectionView with DataTemplate
```xml
<CollectionView SelectionMode="Single" SelectionChanged="OnSelectionChanged">
    <CollectionView.ItemTemplate>
        <DataTemplate x:DataType="local:TodoItem">
            <Border BackgroundColor="{StaticResource CardBackground}">
                <Label Text="{Binding Title}" />
            </Border>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

### Grid with Star Rows (Expanding Editor)
```xml
<Grid RowDefinitions="Auto,Auto,*,Auto,Auto" Padding="20">
    <!-- Row 0: Title section (Auto) -->
    <!-- Row 1: Notes label (Auto) -->
    <!-- Row 2: Editor - expands to fill (*) -->
    <!-- Row 3: Status section (Auto) -->
    <!-- Row 4: Created date (Auto) -->
</Grid>
```

## Building and Running

```bash
# From the maui-linux-push directory
cd samples/TodoApp
dotnet publish -c Release -r linux-arm64

# Run on Linux
./bin/Release/net9.0/linux-arm64/publish/TodoApp
```

## Controls Demonstrated

| Control | Usage |
|---------|-------|
| NavigationPage | App navigation container |
| ContentPage | Individual screens |
| CollectionView | Task list with selection |
| Grid | Page layouts |
| VerticalStackLayout | Vertical grouping |
| HorizontalStackLayout | Horizontal grouping |
| Label | Text display |
| Entry | Single-line input |
| Editor | Multi-line input |
| Button | Toolbar actions |
| Border | Card styling with rounded corners |
| CheckBox | Completion toggle |
| BoxView | Visual separators |

## License

MIT License - See repository root for details.
