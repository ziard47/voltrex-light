// Voltrex Light Website Script
document.addEventListener('DOMContentLoaded', () => {
  // 1. Interactive FAQ Accordion
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

  // 2. Mobile Navigation Menu Toggle
  const mobileMenuBtn = document.getElementById('mobileMenuBtn');
  const mobileNavMenu = document.getElementById('mobileNavMenu');

  if (mobileMenuBtn && mobileNavMenu) {
    const toggleMobileMenu = () => {
      const isOpen = mobileNavMenu.classList.contains('open');
      if (isOpen) {
        mobileNavMenu.classList.remove('open');
        mobileMenuBtn.setAttribute('aria-expanded', 'false');
        mobileMenuBtn.innerHTML = '<i class="fa-solid fa-bars"></i>';
      } else {
        mobileNavMenu.classList.add('open');
        mobileMenuBtn.setAttribute('aria-expanded', 'true');
        mobileMenuBtn.innerHTML = '<i class="fa-solid fa-xmark"></i>';
      }
    };

    mobileMenuBtn.addEventListener('click', toggleMobileMenu);

    // Close mobile menu when clicking any nav link
    const mobileLinks = mobileNavMenu.querySelectorAll('.mobile-nav-link, a');
    mobileLinks.forEach(link => {
      link.addEventListener('click', () => {
        mobileNavMenu.classList.remove('open');
        mobileMenuBtn.setAttribute('aria-expanded', 'false');
        mobileMenuBtn.innerHTML = '<i class="fa-solid fa-bars"></i>';
      });
    });

    // Close on outside click
    document.addEventListener('click', (e) => {
      if (mobileNavMenu.classList.contains('open') &&
          !mobileNavMenu.contains(e.target) &&
          !mobileMenuBtn.contains(e.target)) {
        mobileNavMenu.classList.remove('open');
        mobileMenuBtn.setAttribute('aria-expanded', 'false');
        mobileMenuBtn.innerHTML = '<i class="fa-solid fa-bars"></i>';
      }
    });

    // Close on Escape key
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && mobileNavMenu.classList.contains('open')) {
        mobileNavMenu.classList.remove('open');
        mobileMenuBtn.setAttribute('aria-expanded', 'false');
        mobileMenuBtn.innerHTML = '<i class="fa-solid fa-bars"></i>';
      }
    });
  }
});

