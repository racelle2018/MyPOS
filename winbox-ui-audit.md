# WinBox reference audit and MyPos density review

Date: 8 October 2026 (Asia/Manila). Scope: audit and documentation only.

## Outcome and evidence

**The supplied screenshot now supports a static WinBox design audit at 1366×768.** Its strongest transferable traits are short controls, closely aligned form rows, a small toolbar/header stack, thin separators, and minimal decoration around the table. MyPos should adopt these density principles while keeping its readable 13-DIP body text and larger cashier actions.

The screenshot shows the disconnected **WinBox 4.4** connection/address-list window, not a connected router workspace. The table is empty. It does not establish how populated router tables, configuration dialogs, or collapsible sections behave. The visible error banner is pre-existing screenshot content; no connection attempt was made by this audit.

Evidence source: user-supplied `codex-clipboard-024b1f57-eb79-4cf5-80d4-4ad5ce1cf032.png`, decoded dimensions **1366×768 pixels**. Pixel samples below were taken from that file. Display scaling, font configuration, and original capture processing are unknown; screenshot pixels must not be treated as WPF DIP automatically.

Evidence labels:

- **Confirmed—image:** directly visible structure/state, or decoded image dimensions. Does not confirm implementation settings.
- **Sampled—image:** exact RGB at stated screenshot coordinates, measured from the supplied PNG; not a confirmed application theme token.
- **Estimate—image:** approximate dimensions, padding, radius, font appearance or weight; normally ±1–2 px for edges and ±2–3 px for internal spacing.
- **Confirmed—source:** local MyPos XAML/C# declarations, not runtime screenshots.
- **Estimate—source model:** arithmetic or layout inference; not a measured improvement.
- **Proposal:** recommended MyPos values, not WinBox settings.
- **Unverified:** unavailable evidence, especially appearance settings, hover/selection and resizing transitions.

No WinBox appearance settings were read. No credentials, connections, router configuration changes or MyPos application edits were made. Automated capture repeatedly timed out; text-only accessibility exposed only the title bar and system buttons. The screenshot supplies the visual evidence that those attempts could not retrieve.

## WinBox typography

| Role | Evidence from screenshot | Estimate / uncertainty |
|---|---|---|
| Main UI family | Compact proportional sans serif | Arial/Segoe UI-like appearance; exact family cannot be identified reliably from these glyphs. No confirmed font-family setting |
| Form labels | Right aligned, darker and heavier than field text | Approximately 12–13 px nominal text size; medium/bold appearance, exact numeric weight unknown |
| Input values | Regular sans serif, one line, vertically centered | Approximately 12–13 px nominal size; visible ink around 11–12 px high |
| Buttons | Connect / Connect to RoMON / Save to list use heavier text | Approximately 12–13 px, bold appearance; only 22 px total control height |
| Tabs and toolbar | Small single-line labels; active tab indicated by blue top edge | Approximately 12–13 px; tabs regular-to-medium, no large headings |
| Table headings | Address, User, Group on a shallow header | Approximately 12–13 px, medium appearance; no evidence for body-cell font because table is empty |
| Section heading | Actions uses a small icon and stronger text | Approximately 13 px bold appearance; hierarchy from weight rather than size |
| Auxiliary text | Checkbox labels, status path, error message | Approximately 12–13 px regular; error text white on red |

Font-size estimates describe apparent scale, not an extracted CSS/WPF/Qt font value. Antialiasing, font metrics and display scaling prevent exact point-size determination. The MikroTik logo is branding, not evidence for the UI font.

## WinBox dimensions and organization

Coordinates are approximate image-space positions with origin at the top-left. This is one maximized-looking screenshot with the taskbar visible, not a resize test.

| Element | Estimate—image | Density interpretation |
|---|---|---|
| Native title bar | y=0–23, about 23 px | Ordinary narrow window chrome |
| Bottom Windows taskbar | y=728–767, about 40 px | The app does not have all 768 px for content |
| Address-list outer panel | x≈496–1355, y≈53–718; about 860×665 px | Most right-hand height belongs to the list |
| Left form / right list split | About 496 px / 860 px | Left side contains substantial branding/empty space; do not copy this proportion into POS |
| Tabs | y≈58–85, about 27–28 px high | Shallow integrated strip without a large enclosing card |
| Table toolbar | y≈86–110, about 25 px high | Commands, Find, Filter and view tools share one row |
| Column header | y≈111–136, about 26 px high | Thin separators; no oversized header padding |
| Table body | x≈497–1221, y≈137–694; about 558 px high | Large uninterrupted white data area |
| Table status | y≈695–717, about 23 px | Small count/status strip directly attached to table |
| Actions sidebar | x≈1222–1355, about 134 px wide | Text actions spaced around 22 px apart, no individual button cards |
| Inputs / dropdowns | About 22 px high | Connect field y≈307–328; Login y≈332–353; Password y≈357–378 |
| Standard form row pitch | About 25 px | Only about 3 px between successive 22-px inputs |
| Label-to-input gap | About 16 px | Labels share a right edge near x≈137; fields start x≈154 |
| Input horizontal padding | About 7–8 px | Values begin near x≈161–162 |
| Field widths | Connect/workspace/comment ≈294 px; login/password ≈147 px | Width reflects expected data rather than stretching every field equally |
| Buttons | About 22 px high; Connect ≈75 px wide | Text-sized buttons, with about 6 px gap to the next action |
| Checkboxes | About 14×14 px | Compact desktop pointer targets; inappropriate as a touch target size for POS |
| Section separator | Thin line near y≈530 from x≈48 to448 | One divider separates connection options from saved-entry metadata |
| Corners / borders | Inputs and buttons about 4–6 px radius; borders about 1 px | Tables mostly square; flat surfaces without visible card shadows |
| Data row height | **Unverified** | Empty list provides no basis for measuring populated rows |

The form's major section break is much larger than its row spacing. WinBox is selectively compact: frequent controls are tightly grouped, while branding and the left-side composition use generous space. The reusable lesson is local alignment and low table chrome, not uniformly eliminating whitespace.

## Colors and visible states

| Surface/state | Sampled—image RGB | Sample coordinate / interpretation |
|---|---|---|
| Page and column-header background | **#EEF0F1** | (470,260), (700,124) |
| Table body | **#FFFFFF** | (700,300) |
| Tab-strip background / table border | **#CDD1D4** | (900,65), (496,300) |
| Unfocused input border | **#CDD1D4** | (200,332) |
| Primary button fill | **#1192F0** | (163,470) |
| Focused input border | **#1192F0** | (200,307) |
| Secondary button fill | **#D9DCDD** | (250,480) |
| Visible error banner | **#CB010D** | (60,290) |

**Text colors—visual estimates:** primary labels and values appear black/near-black; primary-button and error text appear white; unavailable commands are pale gray. Exact foreground tokens were not extracted from antialiased text. The screenshot's bright blue is useful for understanding hierarchy, but MyPos can keep its existing darker #2878BD accent for white-text contrast; copying WinBox's blue is not necessary to gain space.

**Confirmed—image:** Connect to has a blue outline and visible caret, supporting a focused-input interpretation. The Saved tab is active-looking with a blue top rule. Remove, Comment and RoMON Neighbors appear disabled/unavailable through gray text. These are visible appearances; programmatic enabled flags were not obtained.

**Unverified:** hover, pressed buttons, text selection, active selected data row, inactive selected row, disabled input appearance, dropdown popup item spacing, focus traversal and interaction animations. No static screenshot proves these transitions. An empty grid cannot establish row selection colors.

## Toolbar, search, dropdowns and resizing

Find is presented as a compact icon/text toolbar command, not a permanently expanded search field. Filter and the `all` dropdown share the right side. Remove and Comment occupy the left. MyPos Products/POS should retain an immediately available search/scan field because entry is frequent; the compact command approach is more suitable for secondary report tools.

Saved, Neighbors and RoMON Neighbors are integrated with the table panel. The Actions sidebar uses a small bold heading and simple text rows. No expanded dropdown or collapsible section is shown. A gear icon is visible at the upper right, but its menu and appearance settings remain unverified.

At **1366×768**, the visible disconnected window fits its form, tab strip, toolbar, column headers, actions and status without obvious clipping. The roughly 558-px table body is about 84% of the 665-px panel height. That favorable ratio is partly due to an empty, simple list and must not be compared directly with a populated financial report. The panel also dedicates 134 px of width to its action sidebar.

No before/after screenshots establish resizing behavior, minimum dimensions, adaptive breakpoints, column expansion or DPI handling. The image confirms one layout at this resolution only. MyPos's scaling risks and pending resize checks are documented below.

## Practical comparison with MyPos

| WinBox screenshot evidence | MyPos source evidence | Recommended transfer |
|---|---|---|
| About 22-px controls, 3-px row gaps | 34–38-DIP general controls; 42-DIP customer fields | Use 30–32 DIP in Reports/Products; retain 40-DIP cashier actions. Do not equate screenshot px with DIP |
| Shallow tabs/toolbar/header | Reports adds padded tab/date/metric/reconciliation/table panels | Flatten stacked containers before reducing font sizes |
| Mostly one small text scale; weight supplies hierarchy | 13-DIP body, 19–21 metric values, 25 total | Keep body font; reserve larger type for meaningful totals |
| Thin neutral borders, flat backgrounds | Radius 6–8, multiple separately bordered cards | Consolidate surfaces and use fewer borders; radius changes alone yield little space |
| Label alignment and context-sized fields | POS has three rows of transaction detail | Align persistent fields and disclose optional details with visible summaries |
| Visible focus and unavailable commands | Focused selection is explicit; inactive selection lacks explicit shared trigger | Preserve clear focus; add a deliberate inactive-selection state after approval |

The detailed MyPos source review and proposed WPF token table below remain recommendations, not exact copies of WinBox. The most valuable changes are flattening Reports chrome, reducing Products toolbar gaps, and reclaiming POS optional-detail/status space. No source changes have been made.

## Confirmed MyPos implementation

Sources are relative to `D:\MyPos`; named resources and controls provide stable lookup anchors.

| Area | Confirmed—source | Implication |
|---|---|---|
| Framework | `src/MyPos.Desktop/MyPos.Desktop.csproj`: WPF, net10.0-windows, MaterialDesignThemes **5.3.2** | Retain installed Material templates and add scoped compact styles |
| Theme | `App.xaml`: Light BundledTheme, MaterialDesign2.Defaults | Audit against Material Design 2 resources actually used |
| Typeface | `App.xaml`, `UiFontFamily`: bundled **Wix Madefor Text** | Family is explicit; successful runtime font resolution was not observed |
| Base type | Window/UserControl 13; buttons 13; page title 20 SemiBold; section heading 12 SemiBold; field hint 12 | Most body type is already suitably compact |
| Buttons | `MyPosButton`: MinHeight 34, MinWidth 70, padding 10,0, border 1, radius 8 | Icon and small navigation buttons need explicit smaller minimum widths |
| Inputs | TextBox padding 10,4; radius 6; border 1; nonfloating hint globally | No global TextBox height is declared; local sizes matter |
| Dropdown/date | ComboBox MinHeight 32, padding 10,0; DatePicker MinHeight 34 | A smaller Height alone does not overcome MinHeight |
| Tables | RowHeight 30, header 32; header padding 7,5; cell template padding 6,0,6,0 | Dense base already exists; table padding is hard-coded in the template |
| Shell | `MainWindow.xaml`: rail 58; nav buttons 44; header padding 16,8 with 15 SemiBold title; status padding 12,4 with 11 type | Rail is already economical; concentrate on content chrome |
| Window | 1120×680 nominal; MinWidth 760, MinHeight 480; starts maximized | Minimum allowed size is not proof that all pages remain usable there |
| Reports tabs | `ReportsView.xaml`: buttons height 34 inside border padding 12,6, margin 12,10,12,0, radius 8 | Tab strip consumes about 58 DIP vertically including border and top margin |
| Products | `ProductCatalogView.xaml`: page margin 12; search 38 high / font 14; action buttons 34; toolbar bottom gap 10 | Small vertical savings; column width is the larger issue when narrow |
| POS | `PosView.xaml`: page margin 8; panels radius 8; inputs 34 / font 14; customer floating inputs 42; actions 40; total 25 Bold | Preserve transaction readability while reducing surrounding structure |

### Source-confirmed colors and states

`App.xaml` defines background **#F4F7FB**, surface **#FFFFFF**, text **#1F2937**, muted **#475569**, border **#C7D4E2**, accent **#2878BD**, soft accent **#EAF3FB**, danger **#B91C1C**, warning **#92400E**, success **#166534**. Alternating rows are **#FAFCFE**; table border is **#AEBECD**, cell dividers **#E1E7ED**, selected cell divider **#1F659F**. Text selection is **#C6E1F7**. These are source values, not screen samples.

Focused selected rows/cells explicitly use accent blue and white text through `IsSelected` plus `IsKeyboardFocusWithin` triggers. `GridCellText` supports white selected text. There is **no explicit inactive-selection brush trigger in the inspected shared row/cell styles**. Retained selection can therefore lose its custom blue treatment when focus leaves. The final inherited/runtime appearance needs verification. This differs from the older `agent.md` description of pale-blue selection and a muted inactive state; current source takes precedence.

`MyPosFocusVisual` draws a 2-DIP accent border with radius 6. Buttons have radius 8, so corner treatment is not uniform. Hover and disabled rendering largely come from the inherited Material templates; exact rendered colors were not inspected. Do not replace these templates just to obtain density.

`Controls/DataGridBehaviors.cs` clears selection on outside clicks/focus changes, except button targets and focus moving into other windows. This means a toolbar action may retain a selected row whose custom focused highlight has disappeared. Selection state and appearance should be designed together.

`ReportsView.xaml.cs` implements the two report choices as buttons, assigning the active one accent/white; these are not native TabItems. POS has conditionally visible print-failure, shift and discount panels, but the inspected layout has no user-operated Expander for optional transaction details.

## Recommended WPF density tokens — proposals

All dimensions and FontSize values below are **WPF device-independent pixels (DIP)**, not typography points or physical screen pixels. 13 DIP corresponds to 9.75 pt. At 125% scaling, 32 DIP occupies about 40 physical pixels. These are proposed values for MyPos, not inferred WinBox defaults.

| Token / use | Recommended value | Treatment |
|---|---|---|
| Font family | Keep Wix Madefor Text | Verify bundled family and peso glyph at runtime; no font swap needed for density |
| Body, table cells, buttons, inputs | 13, Normal | Use 14 for POS scan input if it improves cashier readability |
| Field labels and secondary text | 12, Normal; group labels SemiBold | Avoid making critical labels 10–11 just to fit |
| Page title / section title | 16 SemiBold / 13 SemiBold | Existing shell title 15 can stay; avoid repeated large page titles |
| Table headings | 12–13 SemiBold | Single line; no unnecessary uppercase |
| Summary values / POS total | 18–20 SemiBold / 24–26 Bold | Preserve monetary hierarchy |
| Read-only report/product row | 28 high | Offer 30 as comfortable mode; test baseline and glyph clipping |
| POS cart row | 30–32 high | Retain 30 initially; larger if direct editing/touch is expected |
| Table header | 28–30 high | 6 DIP horizontal cell padding; vertical centering |
| Desktop toolbar button | MinHeight 30–32; padding 8,0 | Explicit compact style must also lower inherited MinHeight |
| Icon / previous-next button | 30–32 square, MinWidth 0 | Tooltip and accessible name; never inherit 70-wide minimum |
| Text/search/dropdown/date control | 32 high; padding 8,3 where template allows | Keep persistent labels; verify Material hint/error layout |
| POS primary actions | 40 high; 44 for touch-oriented operation | Keep Pay prominent; do not shrink cashier targets to report density |
| Tabs / compact section header | 30–32 high | One strip; no padded card around the strip |
| Page inset / panel inset | 8 / 8 | Table panels can omit inner padding |
| Related control gap / section gap | 4–6 / 8 | Use consistent spacing instead of nested 10–16 gaps |
| Border / radius | 1 / 4 | Tables 0–2 radius; cards 4; radius itself saves little space |
| Page / surface / alternate row | #F4F7FB / #FFFFFF / #FAFCFE | Retain existing palette |
| Text / secondary / border | #1F2937 / #475569 / #C7D4E2 | Retain existing palette |
| Hover | #EAF3FB background | Subtle indication, no size change |
| Keyboard focus | #2878BD, 2-DIP outline | Remain visible without shifting layout |
| Active selection | #2878BD background, #FFFFFF text | Apply consistently to template columns and status text |
| Inactive retained selection | #E2E8F0 background, #1F2937 text | Explicit trigger; must remain distinct from hover/unselected |
| Disabled | #F1F5F9 background, #64748B text, #CBD5E1 border | Proposal to validate against templates; explain disabled actions with tooltips |
| Validation / warnings | Existing danger/warning/success tokens | Keep receipt requirements and failure messages visible |

Use named compact styles for Reports and Products before changing global defaults. Local Height values and Material minimums must be reviewed together. Preserve keyboard navigation, validation, hints, selection, and virtualization. Avoid placing DataGrids inside an outer vertical ScrollViewer or scaling entire screens with a Viewbox.

## Changes that would reclaim space

### Reports — highest expected vertical gain

`DailySalesView.xaml` stacks date navigation (36-high controls plus 20 vertical padding), metric cards (MinHeight 80 plus 8 bottom margin), reconciliation (two text lines plus 16 padding), and a Sales title/help/actions header inside a padded table panel. This sits below the report-choice strip.

1. Replace the padded report-choice card with a 32-high tab strip: approximately **26 DIP saved** against its current ~58-DIP footprint.
2. Flatten the date toolbar to 32-high controls, 4 top/bottom padding and a 6-DIP section gap: approximately **20 DIP saved** against ~68 today.
3. Replace five 80-high cards with one aligned 52-high summary strip, plus 6-DIP gap: approximately **30 DIP saved**. Keep all five metrics and full values accessible.
4. Put reconciliation label and counts in one line at desktop width; retain or expand the missing-receipt warning when relevant. Potential **14–22 DIP saved**, dependent on wrapping.
5. Put Sales title and action buttons on one compact line; move explanatory help to an accessible tooltip or empty-state text. Remove the table's 16-DIP side padding and reduce vertical inset. Potential **12–24 DIP saved**.

**Estimate—source model:** roughly **100–120 DIP**, or about **3–4 extra 30-DIP rows**, with ordinary one-line content. This is not a measured rendered improvement; wrapping and validation can consume the gain.

Specific issue: previous/next date buttons request Width=36 but inherit MinWidth=70 from `MyPosButton`. A scoped icon-button style with MinWidth=0 would reclaim **34 DIP each** under normal WPF sizing behavior. Check other narrow buttons, including Audit Log's Width=60 “All.”

The sales columns total **1055 DIP minimum** before scrollbar/border allowance. `SizeToCells` monetary columns can expand further for large amounts. At narrow widths, choose visible columns with a user-controlled column picker or retain horizontal scrolling; do not silently truncate money. Customer can become a star column with a minimum. Preserve access to gross, discount, VAT, net and void data.

For Audit Log, keep the primary date/search filters visible; put less-used action/user filters in a compact expandable area with an active-filter summary. Collapse empty validation-message space. Keep errors visible and reset/filter state understandable.

### Products — modest vertical gains, useful width gains

The page already has one toolbar and a full-height grid. Change page inset 12→8, search 38→32, and toolbar gap 10→6: about **18 DIP vertical gain**. Reducing rows 30→28 is a separate option: a 450-DIP body fits 15 current rows versus 16 proposed rows. Avoid claiming the same gain for all viewport heights.

Fixed columns consume **725 DIP** before Product Description, scrollbar and borders. At roughly 1000 DIP of available grid width this leaves only about 250 DIP for the description. Offer a compact column preset moving infrequent Cost/Category/Status details into a detail panel or column chooser, while keeping price, stock, barcode and description readily available. Keep stock warnings discoverable and never remove permission checks.

`ToolbarGrid_SizeChanged` already stacks actions below search below **800 DIP**. Preserve that behavior and add a useful search minimum. The stock/status text must be checked on selected rows; semantic colors and white selected text should not conflict.

### POS — reduce optional detail space, preserve selling controls

The left pane contains three transaction rows: invoice/payment/type, customer/address (42-high fields with 14 top margin), and discount controls. Keep invoice and scan/search always visible. Propose a “Customer and discount details” disclosure with a compact summary when collapsed; expand automatically for validation errors or when a discount requiring details is selected. Preserve customer/address and discount data when collapsed. Do not hide a required field behind a closed section.

**Estimate—source model:** collapsing the optional customer and discount rows to a ~30-DIP summary could return roughly **75–85 DIP** to the product list, about 2–3 rows. This requires explicit workflow design, not merely changing Visibility in XAML.

Consolidate POS's local status/clock bar and the shell status bar into one status surface. Estimated recovery **25–30 DIP**, depending on text metrics. Keep failure/shift prompts adjacent to payment and avoid squeezing Pay or the total to obtain this space.

The workspace uses a fixed **1*:1.04*** split. Product fixed columns use 270 DIP; cart fixed columns use 345 DIP, leaving the star description column to absorb narrowing. Both grids explicitly disable horizontal scrolling. Add minimum useful description widths and a width-aware compact column plan before making rows smaller. A pane splitter with sensible limits could help desktop users, but must not make payment inaccessible.

`PosView_SizeChanged` currently only hides “Sale details” below **470 DIP height**. It does not reflow columns or transaction fields. Add height-aware optional detail collapse and a narrow-width layout proposal; keep independently scrolling product/cart lists.

## 1366×768 assessment

No live resize or DPI measurement succeeded. The following is a planning model, not acceptance evidence.

| Display scaling | Entire screen in approximate DIP, before taskbar/window chrome | Source-derived risk |
|---|---|---|
| 100% | 1366×768 | Reports loses several table rows to stacked chrome; POS likely benefits most from collapsed optional details |
| 125% | 1093×614 | Reports' 1055-DIP minimum columns exceed the remaining width after rail/insets; POS descriptions become narrow |
| 150% | 911×512 | Reports toolbar may cross its 900-DIP header breakpoint; Products may approach its 800-DIP toolbar breakpoint; vertical room is tight |

Subtract actual taskbar, title bar, shell header/footer, 58-DIP rail and page insets before computing usable content. Breakpoints use control widths in DIP, not the advertised monitor width. MainWindow's minimum outer size does not guarantee a viable POS workspace at elevated scaling.

## Verification before implementation approval

The supplied screenshot now supports the static WinBox comparison above. Complete the remaining appearance-settings and interaction-state inspection when capture works, without connecting or submitting credentials. Record appearance settings verbatim and note theme, scale, client size and safe surface inspected. Any connected-only control that cannot be inspected within scope stays unverified.

For a later MyPos implementation, compare baseline/proposal at 1366×768 with 100%, 125% and 150% scaling, and at the minimum supported window size. Record fully visible table rows and minimum description widths. Exercise long product names, large peso values, empty and populated tables, keyboard focus, hover, active/inactive selection, disabled actions, opened dropdowns, required invoice errors, discounts, closed shifts and print failures. Check toolbar wrapping and that payment remains reachable. These are pending checks, not tests run in this audit.

No application edits are authorized by this document. Wait for the user's implementation decision.

## Live inspection retry — 8 October 2026

At the user's request, another inspection was attempted. WinBox 4.4 was again found and activated. Screenshot capture again failed with `FrameArrived timed out: timed out waiting on channel`.

A refreshed, text-only accessibility request succeeded, but exposed only the window, title bar, System menu, Minimise, Maximise and Close. It exposed no application content, appearance preferences, font information or control dimensions. An attempt to invoke the exposed Maximise control failed with `coordinate input geometry is unavailable`; no successful resize was confirmed. Inspection stopped rather than guessing coordinates.

This confirms that the blocker is inspection-tool access to the rendered interface, not a missing MikroTik account. No credentials were entered and no connection or router configuration action was taken. At the time of this retry, all WinBox appearance findings remained unverified; the subsequent supplied screenshot now supports the static findings at the beginning of this report. User-provided screenshots of the disconnected main window and any locally accessible appearance settings can support a visual audit; exact font settings require readable settings evidence, and interaction states require corresponding screenshots or working live capture.

