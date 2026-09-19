<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output omit-xml-declaration="yes"/>
<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>
 
    <xsl:template match="NPCCharacter[@id='nord_drengr']"/>
    <xsl:template match="NPCCharacter[@id='nord_axe_warrior']"/>
    <xsl:template match="NPCCharacter[@id='nord_spear_warrior']"/>
    <xsl:template match="NPCCharacter[@id='nord_hew-bearer']"/>
    <xsl:template match="NPCCharacter[@id='nord_boandi']"/>
    <xsl:template match="NPCCharacter[@id='nord_freeman_archer']"/>
    <xsl:template match="NPCCharacter[@id='nord_marksman']"/>
    <xsl:template match="NPCCharacter[@id='nord_thegn']"/>
    <xsl:template match="NPCCharacter[@id='nord_jarlsmann']"/>
    <xsl:template match="NPCCharacter[@id='nord_vargr']"/>
    <xsl:template match="NPCCharacter[@id='nord_skjaldbrestir']"/>
    <xsl:template match="NPCCharacter[@id='nord_hirdmann']"/>
    <xsl:template match="NPCCharacter[@id='nord_huscarl']"/>
    
</xsl:stylesheet>
