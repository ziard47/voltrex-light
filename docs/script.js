// Voltrex Light Website Script - Interactive FAQ Accordion
document.addEventListener('DOMContentLoaded', () => {
  const faqQuestions = document.querySelectorAll('.faq-question');
  
  faqQuestions.forEach(button => {
    button.addEventListener('click', () => {
      const item = button.parentElement;
      const isOpen = item.classList.contains('open');
      
      // Close other accordion items
      document.querySelectorAll('.faq-item').forEach(other => {
        if (other !== item) other.classList.remove('open');
      });

      // Toggle clicked item
      if (!isOpen) {
        item.classList.add('open');
      } else {
        item.classList.remove('open');
      }
    });
  });
});

