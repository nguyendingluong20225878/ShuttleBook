export function CustomerFeatureHero({ label, title }: { label: string; title: string }) {
  return <section className="customer-feature-hero">
    <div className="customer-feature-hero-copy"><p>{label}</p><h1>{title}</h1></div>
  </section>;
}
