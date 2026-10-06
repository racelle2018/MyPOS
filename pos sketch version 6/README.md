# MyPos POS — sketch version 6

This is a **static design preview only**. The WPF POS screen has not been changed.

- [Desktop, normal sale](desktop.png): Total Amount, net, VAT and item count share one short, full-width summary bar inside Current Sale.
- [Desktop, discount applied](desktop-discount.png): a second summary line appears only when a discount is applied.
- [Compact, initial view](compact.png) and [compact, scrolled to Current Sale](compact-sale.png): the summary values wrap onto two rows, followed by wrapped Hold/Recall/Clear/Pay buttons.
- [Responsive HTML sketch](index.html)

Products still contains Search/Scan above Product Code, Description, Price and Stock. Current Sale still contains the cart, summary and action buttons. The bar now uses the space that was blank beside the old right-aligned total card and gives the cart more vertical room. The open cart area above the summary is intentional space for added items; it is not filled with unrelated controls. Cart rows scroll separately from the summary and buttons.

The sample products and figures are illustrative. No application behavior, database, or calculation has been changed.
